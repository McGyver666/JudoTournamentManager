using ShiaiManager.Api.Data;
using ShiaiManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace ShiaiManager.Api.Services;

/// <summary>
/// SQLite-backed storage for team-matchday configuration.
/// </summary>
public sealed class SqliteTeamMatchdayStore : ITeamMatchdayStore
{
    private readonly AppDbContext _dbContext;
    private readonly ITeamMatchdayRules _rules;

    /// <summary>
    /// Initializes a new instance of the store.
    /// </summary>
    public SqliteTeamMatchdayStore(AppDbContext dbContext, ITeamMatchdayRules rules)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(rules);
        _dbContext = dbContext;
        _rules = rules;
    }

    /// <inheritdoc />
    public async Task<TeamMatchday?> GetAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await GetTeamMatchdayTournamentAsync(tournamentId, cancellationToken);
        if (tournament is null || !TryGetProfile(tournament, out var profile))
        {
            return null;
        }

        var teams = await _dbContext.TeamMatchdayTeams
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Name)
            .Select(x => new TeamMatchdayTeam(x.Id, x.TournamentId, x.ClubId, x.Name, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
        var weighIns = await _dbContext.MatchdayWeighIns
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.AthleteId)
            .Select(x => new MatchdayWeighIn(x.Id, x.TournamentId, x.AthleteId, x.WeightKg, x.ConfirmedAtUtc))
            .ToArrayAsync(cancellationToken);
        var encounterRecords = await _dbContext.TeamEncounters
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        var encounterIds = encounterRecords.Select(x => x.Id).ToArray();
        var encounterBouts = await _dbContext.EncounterBouts
            .AsNoTracking()
            .Where(x => encounterIds.Contains(x.EncounterId))
            .ToArrayAsync(cancellationToken);
        var fights = await _dbContext.Fights
            .AsNoTracking()
            .Where(x => encounterBouts.Select(bout => bout.FightId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var encounters = encounterRecords
            .Select(encounter => MapEncounter(
                encounter,
                profile,
                encounterBouts.Where(bout => bout.EncounterId == encounter.Id),
                fights))
            .ToArray();

        var weightClassOrder = DeserializeWeightClassOrder(tournament.TeamMatchdayWeightClassOrderJson);
        return new TeamMatchday(tournamentId, profile, teams, weighIns, weightClassOrder, encounters);
    }

    /// <inheritdoc />
    public async Task<TeamMatchdayTeam?> AddTeamAsync(
        Guid tournamentId,
        Guid clubId,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (await GetTeamMatchdayTournamentAsync(tournamentId, cancellationToken) is null
            || !await _dbContext.Clubs.AnyAsync(x => x.Id == clubId && x.TournamentId == tournamentId, cancellationToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var record = new TeamMatchdayTeamRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            ClubId = clubId,
            Name = name.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _dbContext.TeamMatchdayTeams.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TeamMatchdayTeam(record.Id, record.TournamentId, record.ClubId, record.Name, record.CreatedAtUtc, record.UpdatedAtUtc);
    }

    /// <inheritdoc />
    public async Task<MatchdayWeighIn?> ConfirmWeighInAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        CancellationToken cancellationToken)
    {
        if (weightKg is < 1m or > 300m
            || await GetTeamMatchdayTournamentAsync(tournamentId, cancellationToken) is null
            || !await _dbContext.Athletes.AnyAsync(x => x.Id == athleteId && x.TournamentId == tournamentId, cancellationToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var record = await _dbContext.MatchdayWeighIns
            .FirstOrDefaultAsync(x => x.TournamentId == tournamentId && x.AthleteId == athleteId, cancellationToken);

        if (record is null)
        {
            record = new MatchdayWeighInRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                AthleteId = athleteId
            };
            _dbContext.MatchdayWeighIns.Add(record);
        }

        record.WeightKg = weightKg;
        record.ConfirmedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MatchdayWeighIn(record.Id, record.TournamentId, record.AthleteId, record.WeightKg, record.ConfirmedAtUtc);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>?> DrawWeightClassOrderAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await GetTeamMatchdayTournamentAsync(tournamentId, cancellationToken);
        if (tournament is null || !TryGetProfile(tournament, out var profile))
        {
            return null;
        }

        var ruleProfile = _rules.GetProfile(profile);
        if (await _dbContext.EncounterBouts.AnyAsync(x => _dbContext.TeamEncounters
            .Where(encounter => encounter.TournamentId == tournamentId)
            .Select(encounter => encounter.Id)
            .Contains(x.EncounterId), cancellationToken))
        {
            return null;
        }

        var order = Enumerable.Range(0, ruleProfile.WeightClassUpperLimitsKg.Count).ToArray();
        for (var index = order.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (order[index], order[swapIndex]) = (order[swapIndex], order[index]);
        }

        tournament.TeamMatchdayWeightClassOrderJson = JsonSerializer.Serialize(order);
        tournament.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return order;
    }

    /// <inheritdoc />
    public async Task<TeamEncounter?> CreateEncounterAsync(
        Guid tournamentId,
        Guid homeTeamId,
        Guid awayTeamId,
        Guid? tatamiId,
        CancellationToken cancellationToken)
    {
        if (homeTeamId == awayTeamId || await GetTeamMatchdayTournamentAsync(tournamentId, cancellationToken) is null)
        {
            return null;
        }

        var teamIds = await _dbContext.TeamMatchdayTeams
            .Where(x => x.TournamentId == tournamentId && (x.Id == homeTeamId || x.Id == awayTeamId))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        if (teamIds.Length != 2
            || tatamiId.HasValue && !await _dbContext.Tatamis.AnyAsync(x => x.Id == tatamiId && x.TournamentId == tournamentId, cancellationToken))
        {
            return null;
        }

        var displayOrder = (await _dbContext.TeamEncounters
            .Where(x => x.TournamentId == tournamentId)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0) + 1;
        var record = new TeamEncounterRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            TatamiId = tatamiId,
            DisplayOrder = displayOrder,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        _dbContext.TeamEncounters.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new TeamEncounter(
            record.Id, record.TournamentId, record.HomeTeamId, record.AwayTeamId, record.TatamiId,
            record.DisplayOrder, record.NoShowTeamId, record.CreatedAtUtc, 0, 0, 0, 0, 0, 0, null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamLineupEntry>> GetLineupAsync(Guid encounterId, int legNumber, CancellationToken cancellationToken)
    {
        return await _dbContext.TeamLineupEntries
            .AsNoTracking()
            .Where(x => x.EncounterId == encounterId && x.LegNumber == legNumber)
            .OrderBy(x => x.TeamId)
            .ThenBy(x => x.WeightClassIndex)
            .Select(x => new TeamLineupEntry(x.Id, x.EncounterId, x.LegNumber, x.TeamId, x.WeightClassIndex, x.AthleteId))
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TeamMatchdayOperationResult> ReplaceLineupAsync(
        Guid tournamentId,
        Guid encounterId,
        int legNumber,
        Guid teamId,
        IReadOnlyList<TeamLineupAssignment> assignments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        if (legNumber is < 1 or > 2)
        {
            return Failure("InvalidLeg", "Der Durchgang muss 1 oder 2 sein.");
        }

        var tournament = await GetTeamMatchdayTournamentAsync(tournamentId, cancellationToken);
        var encounter = await GetEncounterAsync(tournamentId, encounterId, cancellationToken);
        if (tournament is null || encounter is null || !TryGetProfile(tournament, out var profile))
        {
            return Failure("NotFound", "Der Kampftag oder die Begegnung wurde nicht gefunden.");
        }

        var ruleProfile = _rules.GetProfile(profile);
        if (teamId != encounter.HomeTeamId && teamId != encounter.AwayTeamId)
        {
            return Failure("InvalidTeam", "Die Mannschaft gehört nicht zu dieser Begegnung.");
        }

        if (await _dbContext.EncounterBouts.AnyAsync(x => x.EncounterId == encounterId && x.LegNumber == legNumber, cancellationToken))
        {
            return Failure("LineupLocked", "Die Aufstellung ist nach der Kampfvorbereitung gesperrt.");
        }

        if (assignments.Count != ruleProfile.WeightClassUpperLimitsKg.Count
            || assignments.Select(x => x.WeightClassIndex).Distinct().Count() != assignments.Count
            || assignments.Select(x => x.AthleteId).Distinct().Count() != assignments.Count
            || assignments.Any(x => x.WeightClassIndex < 0 || x.WeightClassIndex >= ruleProfile.WeightClassUpperLimitsKg.Count))
        {
            return Failure("InvalidLineup", "Die Aufstellung muss jede Gewichtsklasse genau einmal und jeden Athleten nur einmal enthalten.");
        }

        var team = await _dbContext.TeamMatchdayTeams
            .FirstOrDefaultAsync(x => x.Id == teamId && x.TournamentId == tournamentId, cancellationToken);
        if (team is null)
        {
            return Failure("InvalidTeam", "Die Mannschaft wurde nicht gefunden.");
        }

        var athletes = await _dbContext.Athletes
            .Where(x => x.TournamentId == tournamentId && x.ClubId == team.ClubId && assignments.Select(a => a.AthleteId).Contains(x.Id))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        var weighIns = await _dbContext.MatchdayWeighIns
            .Where(x => x.TournamentId == tournamentId && assignments.Select(a => a.AthleteId).Contains(x.AthleteId))
            .ToDictionaryAsync(x => x.AthleteId, x => x.WeightKg, cancellationToken);
        if (athletes.Length != assignments.Count
            || assignments.Any(x => !weighIns.TryGetValue(x.AthleteId, out var weightKg)
                || !_rules.CanAssignToWeightClass(ruleProfile, weightKg, x.WeightClassIndex)))
        {
            return Failure("IneligibleLineup", "Alle Athleten benötigen eine gültige Tageswaage für ihre Gewichtsklasse.");
        }

        var existing = await _dbContext.TeamLineupEntries
            .Where(x => x.EncounterId == encounterId && x.LegNumber == legNumber && x.TeamId == teamId)
            .ToArrayAsync(cancellationToken);
        _dbContext.TeamLineupEntries.RemoveRange(existing);
        _dbContext.TeamLineupEntries.AddRange(assignments.Select(x => new TeamLineupEntryRecord
        {
            Id = Guid.NewGuid(),
            EncounterId = encounterId,
            LegNumber = legNumber,
            TeamId = teamId,
            WeightClassIndex = x.WeightClassIndex,
            AthleteId = x.AthleteId
        }));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return TeamMatchdayOperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EncounterBout>?> PrepareEncounterLegAsync(
        Guid tournamentId,
        Guid encounterId,
        int legNumber,
        CancellationToken cancellationToken)
    {
        var tournament = await GetTeamMatchdayTournamentAsync(tournamentId, cancellationToken);
        var encounter = await GetEncounterAsync(tournamentId, encounterId, cancellationToken);
        if (tournament is null || encounter is null || encounter.TatamiId is null || !TryGetProfile(tournament, out var profile))
        {
            return null;
        }

        var ruleProfile = _rules.GetProfile(profile);
        var order = DeserializeWeightClassOrder(tournament.TeamMatchdayWeightClassOrderJson);
        if (order.Count != ruleProfile.WeightClassUpperLimitsKg.Count
            || await _dbContext.EncounterBouts.AnyAsync(x => x.EncounterId == encounterId && x.LegNumber == legNumber, cancellationToken))
        {
            return null;
        }

        var lineup = await GetLineupAsync(encounterId, legNumber, cancellationToken);
        if (lineup.Count != ruleProfile.WeightClassUpperLimitsKg.Count * 2)
        {
            return null;
        }

        var categories = await EnsureProfileCategoriesAsync(tournamentId, ruleProfile, cancellationToken);
        var nextFightNumbers = await _dbContext.Fights
            .Where(x => categories.Values.Contains(x.CategoryId))
            .GroupBy(x => x.CategoryId)
            .ToDictionaryAsync(x => x.Key, x => x.Max(y => y.FightNumber), cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var records = new List<EncounterBoutRecord>(order.Count);
        foreach (var weightClassIndex in order)
        {
            var homeAthleteId = lineup.Single(x => x.TeamId == encounter.HomeTeamId && x.WeightClassIndex == weightClassIndex).AthleteId;
            var awayAthleteId = lineup.Single(x => x.TeamId == encounter.AwayTeamId && x.WeightClassIndex == weightClassIndex).AthleteId;
            var categoryId = categories[weightClassIndex];
            var fight = new FightRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                CategoryId = categoryId,
                BracketType = FightBracketType.Main.ToString(),
                Round = 1,
                FightNumber = nextFightNumbers.GetValueOrDefault(categoryId) + 1,
                WhiteAthleteId = homeAthleteId,
                BlueAthleteId = awayAthleteId,
                Status = FightStatus.Pending.ToString(),
                TatamiId = encounter.TatamiId,
                QueueOrder = null,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            nextFightNumbers[categoryId] = fight.FightNumber;
            _dbContext.Fights.Add(fight);
            records.Add(new EncounterBoutRecord
            {
                Id = Guid.NewGuid(),
                EncounterId = encounterId,
                LegNumber = legNumber,
                WeightClassIndex = weightClassIndex,
                FightId = fight.Id
            });
        }

        _dbContext.EncounterBouts.AddRange(records);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return records.Select(x => new EncounterBout(x.Id, x.EncounterId, x.LegNumber, x.WeightClassIndex, x.FightId)).ToArray();
    }

    /// <inheritdoc />
    public async Task<TeamMatchdayOperationResult> RecordNoShowAsync(
        Guid tournamentId,
        Guid encounterId,
        Guid noShowTeamId,
        CancellationToken cancellationToken)
    {
        var tournament = await GetTeamMatchdayTournamentAsync(tournamentId, cancellationToken);
        var encounter = await GetEncounterAsync(tournamentId, encounterId, cancellationToken);
        if (tournament is null || encounter is null || !TryGetProfile(tournament, out var profile))
        {
            return Failure("NotFound", "Der Kampftag oder die Begegnung wurde nicht gefunden.");
        }

        if (noShowTeamId != encounter.HomeTeamId && noShowTeamId != encounter.AwayTeamId
            || await _dbContext.EncounterBouts.AnyAsync(x => x.EncounterId == encounterId, cancellationToken))
        {
            return Failure("InvalidNoShow", "Ein Nichtantritt kann nur vor der Kampfvorbereitung für eine beteiligte Mannschaft erfasst werden.");
        }

        _ = _rules.GetNoShowResult(profile);
        encounter.NoShowTeamId = noShowTeamId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return TeamMatchdayOperationResult.Success;
    }

    private async Task<TournamentRecord?> GetTeamMatchdayTournamentAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Tournaments.FirstOrDefaultAsync(
            x => x.Id == tournamentId && x.CompetitionMode == CompetitionMode.TeamMatchday.ToString(),
            cancellationToken);
    }

    private bool TryGetProfile(TournamentRecord tournament, out TeamMatchdayProfile profile)
    {
        if (Enum.TryParse<TeamMatchdayProfile>(tournament.TeamMatchdayProfile, out profile))
        {
            _ = _rules.GetProfile(profile);
            return true;
        }

        profile = default;
        return false;
    }

    private static IReadOnlyList<int> DeserializeWeightClassOrder(string? serializedOrder)
    {
        if (string.IsNullOrWhiteSpace(serializedOrder))
        {
            return [];
        }

        return JsonSerializer.Deserialize<int[]>(serializedOrder) ?? [];
    }

    private async Task<TeamEncounterRecord?> GetEncounterAsync(Guid tournamentId, Guid encounterId, CancellationToken cancellationToken)
    {
        return await _dbContext.TeamEncounters.FirstOrDefaultAsync(
            x => x.Id == encounterId && x.TournamentId == tournamentId,
            cancellationToken);
    }

    private async Task<IReadOnlyDictionary<int, Guid>> EnsureProfileCategoriesAsync(
        Guid tournamentId,
        TeamMatchdayRuleProfile profile,
        CancellationToken cancellationToken)
    {
        var gender = profile.Profile is TeamMatchdayProfile.SeniorMen or TeamMatchdayProfile.U16Boys
            ? Gender.Male
            : Gender.Female;
        var ageGroup = profile.FightRules == TeamMatchdayFightRules.U15 ? "U16" : "Senioren";
        var existing = await _dbContext.Categories
            .Where(x => x.TournamentId == tournamentId && x.AgeGroup == ageGroup && x.Gender == gender.ToString())
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < profile.WeightClassUpperLimitsKg.Count; index++)
        {
            var upperLimit = profile.WeightClassUpperLimitsKg[index];
            if (existing.Any(x => x.WeightClassKg == upperLimit))
            {
                continue;
            }

            var category = new CategoryRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                Name = $"{ageGroup} {(gender == Gender.Male ? "M" : "W")} {(upperLimit is null ? "+" : $"-{upperLimit:0}")}",
                AgeGroup = ageGroup,
                Gender = gender.ToString(),
                WeightClassKg = upperLimit,
                RulesetNotes = "NWJV Landesliga",
                MatchDurationSeconds = profile.MatchDurationSeconds,
                GoldenScoreEnabled = true,
                GoldenScoreDurationSeconds = 180,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            existing.Add(category);
            _dbContext.Categories.Add(category);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing
            .OrderBy(x => x.WeightClassKg is null ? decimal.MaxValue : x.WeightClassKg)
            .Select((category, index) => new { index, category.Id })
            .ToDictionary(x => x.index, x => x.Id);
    }

    private TeamEncounter MapEncounter(
        TeamEncounterRecord record,
        TeamMatchdayProfile profile,
        IEnumerable<EncounterBoutRecord> encounterBouts,
        IReadOnlyDictionary<Guid, FightRecord> fights)
    {
        if (record.NoShowTeamId.HasValue)
        {
            var noShowScore = _rules.GetNoShowResult(profile);
            var homeIsAbsent = record.NoShowTeamId == record.HomeTeamId;
            return new TeamEncounter(
                record.Id, record.TournamentId, record.HomeTeamId, record.AwayTeamId, record.TatamiId,
                record.DisplayOrder, record.NoShowTeamId, record.CreatedAtUtc,
                homeIsAbsent ? noShowScore.NonAppearingIndividualWins : noShowScore.OpposingIndividualWins,
                homeIsAbsent ? noShowScore.OpposingIndividualWins : noShowScore.NonAppearingIndividualWins,
                homeIsAbsent ? noShowScore.NonAppearingUnderScore : noShowScore.OpposingUnderScore,
                homeIsAbsent ? noShowScore.OpposingUnderScore : noShowScore.NonAppearingUnderScore,
                homeIsAbsent ? noShowScore.NonAppearingTeamPoints : noShowScore.OpposingTeamPoints,
                homeIsAbsent ? noShowScore.OpposingTeamPoints : noShowScore.NonAppearingTeamPoints,
                homeIsAbsent ? TeamEncounterOutcome.AwayWin : TeamEncounterOutcome.HomeWin);
        }

        var bouts = encounterBouts.Where(bout => fights.ContainsKey(bout.FightId)).Select(bout => fights[bout.FightId]).ToArray();
        var homeWins = bouts.Count(fight => fight.WinnerId == fight.WhiteAthleteId);
        var awayWins = bouts.Count(fight => fight.WinnerId == fight.BlueAthleteId);
        var homeUnderScore = bouts.Sum(fight => fight.WhiteScore);
        var awayUnderScore = bouts.Sum(fight => fight.BlueScore);
        var completed = bouts.Length > 0 && bouts.All(fight => fight.Status == FightStatus.Completed.ToString());
        TeamEncounterOutcome? outcome = completed
            ? _rules.EvaluateEncounter(homeWins, awayWins, homeUnderScore, awayUnderScore)
            : null;
        var homeTeamPoints = outcome switch
        {
            TeamEncounterOutcome.HomeWin => 2,
            TeamEncounterOutcome.Hikiwake => 1,
            _ => 0
        };
        var awayTeamPoints = outcome switch
        {
            TeamEncounterOutcome.AwayWin => 2,
            TeamEncounterOutcome.Hikiwake => 1,
            _ => 0
        };

        return new TeamEncounter(
            record.Id, record.TournamentId, record.HomeTeamId, record.AwayTeamId, record.TatamiId,
            record.DisplayOrder, record.NoShowTeamId, record.CreatedAtUtc, homeWins, awayWins,
            homeUnderScore, awayUnderScore, homeTeamPoints, awayTeamPoints, outcome);
    }

    private static TeamMatchdayOperationResult Failure(string code, string message) => new(false, code, message);
}