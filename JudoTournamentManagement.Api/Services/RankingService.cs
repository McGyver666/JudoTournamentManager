using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Computes provisional category rankings and tournament medal tables from the current bracket state (G-02, G-03).
/// Results are always provisional: placements are derived from whatever fight outcomes are available so far.
/// </summary>
public sealed class RankingService : IRankingService
{
    private readonly AppDbContext _dbContext;

    private static readonly string MainType = FightBracketType.Main.ToString();
    private static readonly string RepechageType = FightBracketType.Repechage.ToString();
    private static readonly string GroupStageType = FightBracketType.GroupStage.ToString();
    private static readonly string CompletedStatus = FightStatus.Completed.ToString();

    /// <summary>Initializes a new service instance.</summary>
    public RankingService(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RankingEntry>> GetCategoryRankingsAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var allFights = await _dbContext.Fights
            .AsNoTracking()
            .Where(f => f.TournamentId == tournamentId && f.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        var fights = allFights.Where(f => !f.IsBye).ToList();

        // Special case: exactly one athlete in category (represented by a completed bye fight)
        // should receive immediate 1st place even before any real fight exists.
        if (fights.Count == 0)
        {
            var completedByeAthleteIds = allFights
                .Where(f => f.IsBye && f.Status == CompletedStatus)
                .SelectMany(f => new[] { f.WhiteAthleteId, f.BlueAthleteId, f.WinnerId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (completedByeAthleteIds.Count == 1)
            {
                var singleAthleteId = completedByeAthleteIds[0];

                var athlete = await _dbContext.Athletes
                    .AsNoTracking()
                    .Where(a => a.Id == singleAthleteId)
                    .Select(a => new { a.Id, a.FirstName, a.LastName, a.ClubId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (athlete is null)
                    return Array.Empty<RankingEntry>();

                var clubName = await _dbContext.Clubs
                    .AsNoTracking()
                    .Where(c => c.Id == athlete.ClubId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

                return new[]
                {
                    new RankingEntry(
                        1,
                        athlete.Id,
                        $"{athlete.LastName}, {athlete.FirstName}",
                        clubName)
                };
            }
        }

        if (fights.Count == 0)
            return Array.Empty<RankingEntry>();

        // Build a lookup from athleteId -> (name, clubId) using Athletes + Clubs tables.
        var athleteIds = fights
            .SelectMany(f => new[] { f.WhiteAthleteId, f.BlueAthleteId, f.WinnerId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToHashSet();

        var athletes = await _dbContext.Athletes
            .AsNoTracking()
            .Where(a => athleteIds.Contains(a.Id))
            .Select(a => new { a.Id, a.FirstName, a.LastName, a.ClubId })
            .ToListAsync(cancellationToken);

        var clubIds = athletes.Select(a => a.ClubId).Distinct().ToHashSet();
        var clubs = await _dbContext.Clubs
            .AsNoTracking()
            .Where(c => clubIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        string AthleteName(Guid id)
        {
            var a = athletes.FirstOrDefault(x => x.Id == id);
            return a is null ? id.ToString() : $"{a.LastName}, {a.FirstName}";
        }

        string ClubName(Guid athleteId)
        {
            var a = athletes.FirstOrDefault(x => x.Id == athleteId);
            if (a is null) return string.Empty;
            return clubs.TryGetValue(a.ClubId, out var name) ? name : string.Empty;
        }

        var mainFights = fights.Where(f => f.BracketType == MainType).ToList();
        var repecFights = fights.Where(f => f.BracketType == RepechageType).ToList();

        var entries = new List<RankingEntry>();

        if (mainFights.Count > 0)
        {
            var maxRound = mainFights.Max(f => f.Round);
            var final = mainFights.FirstOrDefault(f => f.Round == maxRound);

            if (final?.WinnerId is { } gold && final.Status == CompletedStatus)
            {
                entries.Add(new RankingEntry(1, gold, AthleteName(gold), ClubName(gold)));

                var silverId = final.WhiteAthleteId == gold ? final.BlueAthleteId : final.WhiteAthleteId;
                if (silverId.HasValue)
                    entries.Add(new RankingEntry(2, silverId.Value, AthleteName(silverId.Value), ClubName(silverId.Value)));
            }
        }

        // Bronze: winners of highest-round repechage fights.
        if (repecFights.Count > 0)
        {
            var maxRepRound = repecFights.Max(f => f.Round);
            var bronzeFights = repecFights.Where(f => f.Round == maxRepRound && f.Status == CompletedStatus && f.WinnerId.HasValue);

            foreach (var bf in bronzeFights)
            {
                entries.Add(new RankingEntry(3, bf.WinnerId!.Value, AthleteName(bf.WinnerId.Value), ClubName(bf.WinnerId.Value)));
            }
        }

        return entries;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MedalEntry>> GetMedalTableAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.TournamentId == tournamentId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var medalCounts = new Dictionary<Guid, (string name, int gold, int silver, int bronze)>();

        foreach (var categoryId in categories)
        {
            var rankings = await GetCategoryRankingsAsync(tournamentId, categoryId, cancellationToken);

            foreach (var entry in rankings)
            {
                // Resolve club for each ranked athlete.
                var athlete = await _dbContext.Athletes
                    .AsNoTracking()
                    .Where(a => a.Id == entry.AthleteId)
                    .Select(a => new { a.ClubId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (athlete is null) continue;

                var club = await _dbContext.Clubs
                    .AsNoTracking()
                    .Where(c => c.Id == athlete.ClubId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (club is null) continue;

                if (!medalCounts.TryGetValue(club.Id, out var current))
                    current = (club.Name, 0, 0, 0);

                medalCounts[club.Id] = entry.Place switch
                {
                    1 => current with { gold = current.gold + 1 },
                    2 => current with { silver = current.silver + 1 },
                    3 => current with { bronze = current.bronze + 1 },
                    _ => current
                };
            }
        }

        return medalCounts
            .Select(kvp => new MedalEntry(kvp.Key, kvp.Value.name, kvp.Value.gold, kvp.Value.silver, kvp.Value.bronze))
            .OrderByDescending(m => m.Gold)
            .ThenByDescending(m => m.Silver)
            .ThenByDescending(m => m.Bronze)
            .ThenBy(m => m.ClubName)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoundRobinStanding>> GetRoundRobinStandingsAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        // Fights that count toward standings: pure RR uses Main; group stage uses GroupStage
        var fights = await _dbContext.Fights
            .AsNoTracking()
            .Where(f => f.TournamentId == tournamentId
                && f.CategoryId == categoryId
                && !f.IsBye
                && (f.BracketType == MainType || f.BracketType == GroupStageType))
            .ToListAsync(cancellationToken);

        if (fights.Count == 0) return Array.Empty<RoundRobinStanding>();

        // Collect all athlete IDs that appear in these fights
        var athleteIds = fights
            .SelectMany(f => new[] { f.WhiteAthleteId, f.BlueAthleteId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToHashSet();

        var athletes = await _dbContext.Athletes
            .AsNoTracking()
            .Where(a => athleteIds.Contains(a.Id))
            .Select(a => new { a.Id, a.FirstName, a.LastName, a.ClubId })
            .ToListAsync(cancellationToken);

        var clubIds = athletes.Select(a => a.ClubId).Distinct().ToHashSet();
        var clubs = await _dbContext.Clubs
            .AsNoTracking()
            .Where(c => clubIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        string AthleteName(Guid id)
        {
            var a = athletes.FirstOrDefault(x => x.Id == id);
            return a is null ? id.ToString() : $"{a.LastName}, {a.FirstName}";
        }

        string ClubName(Guid athleteId)
        {
            var a = athletes.FirstOrDefault(x => x.Id == athleteId);
            if (a is null) return string.Empty;
            return clubs.TryGetValue(a.ClubId, out var name) ? name : string.Empty;
        }

        // Determine distinct pools: null pool → treat as pool 0 (pure round-robin)
        var poolNumbers = fights
            .Select(f => f.PoolNumber ?? 0)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        var result = new List<RoundRobinStanding>();

        foreach (var pool in poolNumbers)
        {
            var poolFights = fights.Where(f => (f.PoolNumber ?? 0) == pool).ToList();

            // Collect all athletes in this pool
            var poolAthleteIds = poolFights
                .SelectMany(f => new[] { f.WhiteAthleteId, f.BlueAthleteId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            // Compute stats per athlete
            var stats = poolAthleteIds.ToDictionary(
                id => id,
                id => (wins: 0, wazaAri: 0, yuko: 0, shidos: 0));

            foreach (var fight in poolFights.Where(f => f.Status == CompletedStatus && f.WinnerId.HasValue))
            {
                var winner = fight.WinnerId!.Value;
                var loser = fight.WhiteAthleteId == winner ? fight.BlueAthleteId : fight.WhiteAthleteId;

                if (stats.ContainsKey(winner))
                {
                    bool winnerIsWhite = fight.WhiteAthleteId == winner;
                    var (wins, wazaAri, yuko, shidos) = stats[winner];
                    stats[winner] = (
                        wins + 1,
                        wazaAri + (winnerIsWhite ? fight.WhiteWazaAriCount : fight.BlueWazaAriCount),
                        yuko + (winnerIsWhite ? fight.WhiteYukoCount : fight.BlueYukoCount),
                        shidos + (winnerIsWhite ? fight.WhitePenalties : fight.BluePenalties)
                    );
                }

                if (loser.HasValue && stats.ContainsKey(loser.Value))
                {
                    bool loserIsWhite = fight.WhiteAthleteId == loser.Value;
                    var (wins, wazaAri, yuko, shidos) = stats[loser.Value];
                    stats[loser.Value] = (
                        wins,
                        wazaAri + (loserIsWhite ? fight.WhiteWazaAriCount : fight.BlueWazaAriCount),
                        yuko + (loserIsWhite ? fight.WhiteYukoCount : fight.BlueYukoCount),
                        shidos + (loserIsWhite ? fight.WhitePenalties : fight.BluePenalties)
                    );
                }
            }

            // Sort: wins desc → waza-ari desc → yuko desc → shidos asc
            var ranked = stats
                .OrderByDescending(kv => kv.Value.wins)
                .ThenByDescending(kv => kv.Value.wazaAri)
                .ThenByDescending(kv => kv.Value.yuko)
                .ThenBy(kv => kv.Value.shidos)
                .Select((kv, index) => new RoundRobinStanding(
                    kv.Key,
                    AthleteName(kv.Key),
                    ClubName(kv.Key),
                    pool,
                    index + 1,
                    kv.Value.wins,
                    kv.Value.wazaAri,
                    kv.Value.yuko,
                    kv.Value.shidos))
                .ToList();

            result.AddRange(ranked);
        }

        return result;
    }
}
