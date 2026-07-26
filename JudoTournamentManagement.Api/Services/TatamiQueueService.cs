using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed fight queue for a tatami (F-01).
/// A fight is "playable" when it is in progress, or pending with both athletes assigned and is not a bye.
/// </summary>
public sealed class TatamiQueueService : ITatamiQueueService
{
    private readonly AppDbContext _dbContext;

    private static readonly string InProgress = FightStatus.InProgress.ToString();
    private static readonly string Paused = FightStatus.Paused.ToString();
    private static readonly string Pending = FightStatus.Pending.ToString();

    /// <summary>Initializes a new service instance.</summary>
    public TatamiQueueService(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<TatamiQueue?> GetQueueAsync(
        Guid tournamentId,
        Guid tatamiId,
        CancellationToken cancellationToken)
    {
        var tatami = await _dbContext.Tatamis
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tatamiId && t.TournamentId == tournamentId, cancellationToken);
        if (tatami is null) return null;

        var records = await _dbContext.Fights
            .AsNoTracking()
            .Include(f => f.Category)
            .Where(f => f.TournamentId == tournamentId && f.TatamiId == tatamiId)
            .ToListAsync(cancellationToken);

        // In-progress fights first, then ready pending fights. Pending fights honor a manual
        // queue order (QueueOrder) when set, falling back to bracket order (Round, FightNumber).
        var playable = records
            .Where(IsPlayable)
            .OrderBy(f => f.Status == InProgress ? 0 : f.Status == Paused ? 1 : 2)
            .ThenBy(f => f.QueueOrder ?? int.MaxValue)
            .ThenBy(f => f.Round)
            .ThenBy(f => f.FightNumber)
            .Select(MapToModel)
            .ToList();

        return new TatamiQueue(
            tatami.Id,
            tatami.Name,
            playable.ElementAtOrDefault(0),
            playable.ElementAtOrDefault(1),
            playable.ElementAtOrDefault(2),
            playable);
    }

    private static bool IsPlayable(FightRecord f) =>
        f.Status == InProgress
        || f.Status == Paused
        || (f.Status == Pending && !f.IsBye && f.WhiteAthleteId is not null && f.BlueAthleteId is not null);

    /// <summary>
    /// Computes whether the fight is currently in the golden-score overtime phase.
    /// True only when: the fight is active (InProgress or Paused), the category has golden score
    /// enabled, the elapsed fight time has exceeded the regular match duration, AND the fighters
    /// have tied scores (equal waza-ari counts and equal yuko counts).
    /// </summary>
    private static bool ComputeIsGoldenScore(FightRecord r)
    {
        if (r.Status != InProgress && r.Status != Paused) return false;
        if (r.StartedAtUtc is null) return false;
        if (r.Category is null || !r.Category.GoldenScoreEnabled) return false;

        var referenceTime = r.Status == Paused && r.PausedAtUtc is not null
            ? r.PausedAtUtc.Value
            : DateTimeOffset.UtcNow;
        var elapsedSeconds = (referenceTime - r.StartedAtUtc.Value).TotalSeconds;
        if (elapsedSeconds < r.Category.MatchDurationSeconds) return false;

        // Golden score only applies when fighters have equal waza-ari and yuko counts (tied).
        var whiteWazaAri = r.WhiteWazaAriCount;
        var blueWazaAri = r.BlueWazaAriCount;
        var whiteYuko = r.WhiteYukoCount;
        var blueYuko = r.BlueYukoCount;

        return whiteWazaAri == blueWazaAri && whiteYuko == blueYuko;
    }

    private static Fight MapToModel(FightRecord r) =>
        new(r.Id,
            r.TournamentId,
            r.CategoryId,
            Enum.Parse<FightBracketType>(r.BracketType),
            r.Round,
            r.FightNumber,
            r.PoolNumber,
            r.WhiteSourceFightId,
            r.WhiteSourceOutcome is null ? null : Enum.Parse<FightSlotSourceOutcome>(r.WhiteSourceOutcome),
            r.BlueSourceFightId,
            r.BlueSourceOutcome is null ? null : Enum.Parse<FightSlotSourceOutcome>(r.BlueSourceOutcome),
            r.WhiteAthleteId,
            r.BlueAthleteId,
            r.WinnerId,
            r.IsBye,
            Enum.Parse<FightStatus>(r.Status),
            r.TatamiId,
            r.QueueOrder,
            r.WhiteScore,
            r.BlueScore,
            r.WhitePenalties,
            r.BluePenalties,
            r.WhiteIpponCount,
            r.WhiteWazaAriCount,
            r.WhiteYukoCount,
            r.BlueIpponCount,
            r.BlueWazaAriCount,
            r.BlueYukoCount,
            r.PausedAtUtc,
            r.OsaeKomiSide,
            r.OsaeKomiStartedAtUtc,
            r.StartedAtUtc,
            r.CompletedAtUtc,
            r.CreatedAtUtc,
            r.UpdatedAtUtc,
            IsGoldenScore: ComputeIsGoldenScore(r));
}
