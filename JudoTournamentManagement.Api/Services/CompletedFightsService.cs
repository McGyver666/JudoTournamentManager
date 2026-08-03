using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Builds the tournament-wide combat overview from the current fight records, resolving athlete,
/// club, category, and tatami names in a small number of set-based queries.
/// </summary>
public sealed class CompletedFightsService : ICompletedFightsService
{
    private readonly AppDbContext _dbContext;

    private static readonly string CompletedStatus = FightStatus.Completed.ToString();
    private const string UnknownName = "—";

    /// <summary>Initializes a new service instance.</summary>
    public CompletedFightsService(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompletedFightSummary>> GetCompletedFightsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var completedFights = await _dbContext.Fights
            .AsNoTracking()
            .Where(f => f.TournamentId == tournamentId
                        && f.Status == CompletedStatus
                        && !f.IsBye)
            .ToListAsync(cancellationToken);

        // SQLite cannot ORDER BY DateTimeOffset, so order in memory after materializing.
        var fights = completedFights
            .OrderByDescending(f => f.CompletedAtUtc)
            .ThenByDescending(f => f.UpdatedAtUtc)
            .ToList();

        if (fights.Count == 0)
        {
            return Array.Empty<CompletedFightSummary>();
        }

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
        var clubNames = await _dbContext.Clubs
            .AsNoTracking()
            .Where(c => clubIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var categoryIds = fights.Select(f => f.CategoryId).Distinct().ToHashSet();
        var categoryNames = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var tatamiIds = fights
            .Where(f => f.TatamiId.HasValue)
            .Select(f => f.TatamiId!.Value)
            .Distinct()
            .ToHashSet();
        var tatamiNames = await _dbContext.Tatamis
            .AsNoTracking()
            .Where(t => tatamiIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var athletesById = athletes.ToDictionary(a => a.Id);

        string AthleteName(Guid? id)
        {
            if (id is null || !athletesById.TryGetValue(id.Value, out var a))
            {
                return UnknownName;
            }

            return $"{a.LastName}, {a.FirstName}";
        }

        string ClubName(Guid? athleteId)
        {
            if (athleteId is null || !athletesById.TryGetValue(athleteId.Value, out var a))
            {
                return string.Empty;
            }

            return clubNames.TryGetValue(a.ClubId, out var name) ? name : string.Empty;
        }

        string? WinnerSide(FightRecord f)
        {
            if (f.WinnerId is null)
            {
                return null;
            }

            if (f.WinnerId == f.WhiteAthleteId)
            {
                return "White";
            }

            return f.WinnerId == f.BlueAthleteId ? "Blue" : null;
        }

        int? DurationSeconds(FightRecord f)
        {
            if (f.StartedAtUtc is null || f.CompletedAtUtc is null)
            {
                return null;
            }

            var seconds = (int)Math.Round((f.CompletedAtUtc.Value - f.StartedAtUtc.Value).TotalSeconds);
            return seconds < 0 ? null : seconds;
        }

        return fights
            .Select(f => new CompletedFightSummary(
                f.Id,
                f.CategoryId,
                categoryNames.TryGetValue(f.CategoryId, out var catName) ? catName : UnknownName,
                f.BracketType,
                f.Round,
                f.FightNumber,
                f.PoolNumber,
                f.TatamiId,
                f.TatamiId is not null && tatamiNames.TryGetValue(f.TatamiId.Value, out var tName) ? tName : null,
                AthleteName(f.WhiteAthleteId),
                ClubName(f.WhiteAthleteId),
                AthleteName(f.BlueAthleteId),
                ClubName(f.BlueAthleteId),
                WinnerSide(f),
                AthleteName(f.WinnerId),
                f.WhiteScore,
                f.BlueScore,
                f.WhitePenalties,
                f.BluePenalties,
                f.WhiteIpponCount,
                f.WhiteWazaAriCount,
                f.WhiteYukoCount,
                f.BlueIpponCount,
                f.BlueWazaAriCount,
                f.BlueYukoCount,
                f.StartedAtUtc,
                f.CompletedAtUtc!.Value,
                DurationSeconds(f)))
            .ToArray();
    }
}
