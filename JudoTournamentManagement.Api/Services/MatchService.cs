using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Hubs;
using JudoTournamentManagement.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Operates fights on tatamis and keeps bracket progression consistent (F-02, F-03).
/// </summary>
public sealed class MatchService : IMatchService
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogService _auditLog;
    private readonly IHubContext<TournamentHub> _hub;
    private readonly IBracketService _bracketService;
    private readonly IRankingService _rankingService;
    private readonly ILogger<MatchService> _logger;

    private static readonly string MainType = FightBracketType.Main.ToString();
    private static readonly string RepechageType = FightBracketType.Repechage.ToString();
    private static readonly string GroupStageType = FightBracketType.GroupStage.ToString();
    private static readonly string Pending = FightStatus.Pending.ToString();
    private static readonly string InProgress = FightStatus.InProgress.ToString();
    private static readonly string Paused = FightStatus.Paused.ToString();
    private static readonly string Completed = FightStatus.Completed.ToString();

    /// <summary>Initializes a new service instance.</summary>
    public MatchService(
        AppDbContext dbContext,
        IAuditLogService auditLog,
        IHubContext<TournamentHub> hub,
        IBracketService bracketService,
        IRankingService rankingService,
        ILogger<MatchService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(bracketService);
        ArgumentNullException.ThrowIfNull(rankingService);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _auditLog = auditLog;
        _hub = hub;
        _bracketService = bracketService;
        _rankingService = rankingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> AssignTatamiAsync(
        Guid fightId,
        Guid? tatamiId,
        string user,
        CancellationToken cancellationToken)
    {
        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (tatamiId is not null)
        {
            var exists = await _dbContext.Tatamis
                .AnyAsync(t => t.Id == tatamiId && t.TournamentId == fight.TournamentId, cancellationToken);
            if (!exists) return MatchActionResult.InvalidState;
        }

        fight.TatamiId = tatamiId;
        fight.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            fight.TournamentId, user, "FightAssignedToTatami", "Fight", fight.Id,
            $"TatamiId={tatamiId?.ToString() ?? "none"}", cancellationToken);

        await BroadcastFightUpdatedAsync(fight);

        return MatchActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> StartAsync(Guid fightId, string user, CancellationToken cancellationToken)
    {
        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (fight.IsBye || fight.Status != Pending
            || fight.WhiteAthleteId is null || fight.BlueAthleteId is null)
        {
            return MatchActionResult.InvalidState;
        }

        var now = DateTimeOffset.UtcNow;

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == fight.CategoryId, cancellationToken);
        if (category is not null && !category.IsLocked)
        {
            category.IsLocked = true;
            category.UpdatedAtUtc = now;
            _logger.LogInformation(
                "Category {CategoryId} locked because first fight {FightId} has started.",
                category.Id,
                fight.Id);
        }

        fight.Status = InProgress;
        fight.StartedAtUtc = now;
        fight.PausedAtUtc = null;
        fight.OsaeKomiSide = null;
        fight.OsaeKomiStartedAtUtc = null;
        fight.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await BroadcastFightUpdatedAsync(fight);

        return MatchActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> PauseAsync(Guid fightId, string user, CancellationToken cancellationToken)
    {
        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (fight.Status != InProgress) return MatchActionResult.InvalidState;

        var now = DateTimeOffset.UtcNow;
        fight.Status = Paused;
        fight.PausedAtUtc = now;
        fight.OsaeKomiSide = null;
        fight.OsaeKomiStartedAtUtc = null;
        fight.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await BroadcastFightUpdatedAsync(fight);

        return MatchActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> ResumeAsync(Guid fightId, string user, CancellationToken cancellationToken)
    {
        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (fight.Status != Paused || fight.StartedAtUtc is null || fight.PausedAtUtc is null)
            return MatchActionResult.InvalidState;

        var elapsedBeforePause = fight.PausedAtUtc.Value - fight.StartedAtUtc.Value;
        var now = DateTimeOffset.UtcNow;
        fight.Status = InProgress;
        fight.StartedAtUtc = now - elapsedBeforePause;
        fight.PausedAtUtc = null;
        fight.OsaeKomiSide = null;
        fight.OsaeKomiStartedAtUtc = null;
        fight.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await BroadcastFightUpdatedAsync(fight);

        return MatchActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> AdjustScoreAsync(
        Guid fightId,
        string side,
        ScoreType scoreType,
        int delta,
        string user,
        CancellationToken cancellationToken)
    {
        if (delta is not 1 and not -1)
            return MatchActionResult.InvalidState;

        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (fight.Status != InProgress && fight.Status != Paused) return MatchActionResult.InvalidState;

        if (!TryGetSide(side, out var whiteSide)) return MatchActionResult.InvalidState;

        var result = ApplyScoreDelta(fight, whiteSide, scoreType, delta);
        if (result != MatchActionResult.Success) return result;

        fight.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await BroadcastFightUpdatedAsync(fight);

        return MatchActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> RecordScoreAsync(
        Guid fightId,
        int whiteScore,
        int blueScore,
        int whitePenalties,
        int bluePenalties,
        string user,
        CancellationToken cancellationToken)
    {
        if (whiteScore < 0 || blueScore < 0 || whitePenalties < 0 || bluePenalties < 0)
            return MatchActionResult.InvalidState;

        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (fight.Status != InProgress && fight.Status != Paused) return MatchActionResult.InvalidState;

        fight.WhiteScore = whiteScore;
        fight.BlueScore = blueScore;
        fight.WhitePenalties = whitePenalties;
        fight.BluePenalties = bluePenalties;
        fight.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await BroadcastFightUpdatedAsync(fight);

        return MatchActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> StartOsaeKomiAsync(
        Guid fightId,
        string side,
        string user,
        CancellationToken cancellationToken)
    {
        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (fight.Status != InProgress || fight.OsaeKomiStartedAtUtc is not null) return MatchActionResult.InvalidState;
        if (!TryGetSide(side, out var whiteSide)) return MatchActionResult.InvalidState;

        fight.OsaeKomiSide = whiteSide ? "White" : "Blue";
        fight.OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow;
        fight.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await BroadcastFightUpdatedAsync(fight);

        return MatchActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> StopOsaeKomiAsync(Guid fightId, string user, CancellationToken cancellationToken)
    {
        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (fight.OsaeKomiStartedAtUtc is null || fight.OsaeKomiSide is null) return MatchActionResult.InvalidState;

        // Capture hold duration and side before clearing the timer fields.
        var holdSeconds = (int)Math.Ceiling((DateTimeOffset.UtcNow - fight.OsaeKomiStartedAtUtc.Value).TotalSeconds);
        var holderIsWhite = fight.OsaeKomiSide == "White";

        // Load tournament Osae-komi rule settings.
        var tournament = await _dbContext.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == fight.TournamentId, cancellationToken);

        var ipponSeconds    = tournament?.OsaeKomiIpponSeconds    ?? 20;
        var wazaAriSeconds  = tournament?.OsaeKomiWazaAriSeconds  ?? 10;
        var yukoSeconds     = tournament?.OsaeKomiYukoSeconds     ?? 5;
        var yukoEnabled     = tournament?.OsaeKomiYukoEnabled     ?? true;

        var holderHasWazaAri = holderIsWhite
            ? fight.WhiteWazaAriCount > 0
            : fight.BlueWazaAriCount > 0;

        // Determine which score to award based on DJB hold-down rules.
        ScoreType? scoreToAward = null;
        if (holdSeconds >= ipponSeconds)
        {
            scoreToAward = ScoreType.Ippon;
        }
        else if (holderHasWazaAri && holdSeconds >= wazaAriSeconds)
        {
            // Second Waza-ari converts to Ippon per DJB rules.
            scoreToAward = ScoreType.Ippon;
        }
        else if (holdSeconds >= wazaAriSeconds)
        {
            scoreToAward = ScoreType.WazaAri;
        }
        else if (yukoEnabled && holdSeconds >= yukoSeconds)
        {
            scoreToAward = ScoreType.Yuko;
        }

        fight.OsaeKomiSide = null;
        fight.OsaeKomiStartedAtUtc = null;
        fight.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (scoreToAward is not null)
        {
            ApplyScoreDelta(fight, holderIsWhite, scoreToAward.Value, 1);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await BroadcastFightUpdatedAsync(fight);

        return MatchActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> ConfirmResultAsync(
        Guid fightId,
        Guid winnerId,
        string user,
        CancellationToken cancellationToken)
    {
        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (fight.Status != InProgress && fight.Status != Paused) return MatchActionResult.InvalidState;
        if (winnerId != fight.WhiteAthleteId && winnerId != fight.BlueAthleteId)
            return MatchActionResult.WinnerNotParticipant;

        var now = DateTimeOffset.UtcNow;
        fight.WinnerId = winnerId;
        fight.Status = Completed;
        fight.CompletedAtUtc = now;
        fight.PausedAtUtc = null;
        fight.OsaeKomiSide = null;
        fight.OsaeKomiStartedAtUtc = null;
        fight.UpdatedAtUtc = now;

        await RecalculateProgressionAsync(fight.CategoryId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            fight.TournamentId, user, "ResultConfirmed", "Fight", fight.Id,
            $"WinnerId={winnerId}; Score={fight.WhiteScore}:{fight.BlueScore}", cancellationToken);

        _ = _hub.Clients.Group(fight.TournamentId.ToString())
            .SendAsync("CategoryFightsUpdated",
                new { tournamentId = fight.TournamentId, categoryId = fight.CategoryId },
                CancellationToken.None);

        // Auto-generate knockout bracket when all group-stage fights are done
        await TryAutoGenerateKnockoutAsync(fight.CategoryId, fight.TournamentId, cancellationToken);

        _logger.LogInformation("Fight {FightId} confirmed, winner {WinnerId}.", fightId, winnerId);
        return MatchActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<MatchActionResult> CorrectResultAsync(
        Guid fightId,
        Guid newWinnerId,
        string user,
        CancellationToken cancellationToken)
    {
        var fight = await _dbContext.Fights.FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);
        if (fight is null) return MatchActionResult.FightNotFound;

        if (fight.Status != Completed || fight.IsBye) return MatchActionResult.InvalidState;
        if (newWinnerId != fight.WhiteAthleteId && newWinnerId != fight.BlueAthleteId)
            return MatchActionResult.WinnerNotParticipant;

        var previousWinnerId = fight.WinnerId;
        if (previousWinnerId == newWinnerId) return MatchActionResult.Success;

        fight.WinnerId = newWinnerId;
        fight.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await RecalculateProgressionAsync(fight.CategoryId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            fight.TournamentId, user, "ResultCorrected", "Fight", fight.Id,
            $"PreviousWinnerId={previousWinnerId}; NewWinnerId={newWinnerId}", cancellationToken);

        _ = _hub.Clients.Group(fight.TournamentId.ToString())
            .SendAsync("CategoryFightsUpdated",
                new { tournamentId = fight.TournamentId, categoryId = fight.CategoryId },
                CancellationToken.None);

        _logger.LogInformation(
            "Fight {FightId} corrected: {Previous} -> {New}.", fightId, previousWinnerId, newWinnerId);
        return MatchActionResult.Success;
    }

    /// <summary>
    /// Recomputes the athletes of every derived fight (round &gt;= 2 main fights and the repechage fight)
    /// purely from the winners and losers of their source fights. Round-1 athlete assignments are never touched.
    /// Group-stage fights are skipped — they have no bracket progression.
    /// This keeps the bracket consistent after both confirmation and correction.
    /// </summary>
    private async Task RecalculateProgressionAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var fights = await _dbContext.Fights
            .Where(f => f.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        var drawFormat = await _dbContext.Categories
            .AsNoTracking()
            .Where(category => category.Id == categoryId)
            .Select(category => category.DrawFormat)
            .FirstOrDefaultAsync(cancellationToken);

        if (drawFormat == BracketFormat.DoubleElimination.ToString())
        {
            RecalculateDoubleEliminationProgression(fights);
            return;
        }

        var main = fights.Where(f => f.BracketType == MainType).ToList();
        if (main.Count == 0) return;

        var maxRound = main.Max(f => f.Round);

        FightRecord? FindMain(int round, int fightNumber) =>
            main.FirstOrDefault(f => f.Round == round && f.FightNumber == fightNumber);

        // Derived main fights: each slot comes from the winner of a source fight in the previous round.
        for (int round = 2; round <= maxRound; round++)
        {
            foreach (var fight in main.Where(f => f.Round == round))
            {
                var whiteSource = FindMain(round - 1, fight.FightNumber * 2 - 1);
                var blueSource = FindMain(round - 1, fight.FightNumber * 2);

                fight.WhiteAthleteId = WinnerOf(whiteSource);
                fight.BlueAthleteId = WinnerOf(blueSource);
                fight.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        // Repechage (3rd-place fight): the two semi-final losers.
        var repechage = fights.FirstOrDefault(f => f.BracketType == RepechageType);
        if (repechage is not null && maxRound >= 2)
        {
            var semifinalRound = maxRound - 1;
            repechage.WhiteAthleteId = LoserOf(FindMain(semifinalRound, 1));
            repechage.BlueAthleteId = LoserOf(FindMain(semifinalRound, 2));
            repechage.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private static void RecalculateDoubleEliminationProgression(List<FightRecord> fights)
    {
        var byId = fights.ToDictionary(fight => fight.Id);

        foreach (var fight in fights
                     .Where(fight => fight.WhiteSourceFightId.HasValue || fight.BlueSourceFightId.HasValue)
                     .OrderBy(fight => fight.FightNumber))
        {
            bool whiteResolved = TryResolveDoubleEliminationSlot(
                byId, fight.WhiteSourceFightId, fight.WhiteSourceOutcome, out var whiteAthleteId);
            bool blueResolved = TryResolveDoubleEliminationSlot(
                byId, fight.BlueSourceFightId, fight.BlueSourceOutcome, out var blueAthleteId);

            if (!whiteResolved || !blueResolved)
            {
                ResetDerivedFight(fight);
                continue;
            }

            bool slotsChanged = fight.WhiteAthleteId != whiteAthleteId
                || fight.BlueAthleteId != blueAthleteId;
            if (slotsChanged)
            {
                ResetDerivedFight(fight);
                fight.WhiteAthleteId = whiteAthleteId;
                fight.BlueAthleteId = blueAthleteId;
            }

            if (whiteAthleteId is null || blueAthleteId is null)
            {
                CompleteBye(fight, whiteAthleteId ?? blueAthleteId);
            }
        }
    }

    private static bool TryResolveDoubleEliminationSlot(
        IReadOnlyDictionary<Guid, FightRecord> fights,
        Guid? sourceFightId,
        string? sourceOutcome,
        out Guid? athleteId)
    {
        athleteId = null;
        if (sourceFightId is not { } sourceId
            || sourceOutcome is null
            || !fights.TryGetValue(sourceId, out var sourceFight)
            || sourceFight.Status != Completed)
        {
            return false;
        }

        if (!Enum.TryParse<FightSlotSourceOutcome>(sourceOutcome, out var outcome))
        {
            return false;
        }

        athleteId = outcome switch
        {
            FightSlotSourceOutcome.Winner => sourceFight.WinnerId,
            FightSlotSourceOutcome.Loser when sourceFight.WinnerId == sourceFight.WhiteAthleteId
                => sourceFight.BlueAthleteId,
            FightSlotSourceOutcome.Loser => sourceFight.WhiteAthleteId,
            _ => null
        };
        return true;
    }

    private static void ResetDerivedFight(FightRecord fight)
    {
        if (fight.Status == Pending
            && !fight.IsBye
            && fight.WhiteAthleteId is null
            && fight.BlueAthleteId is null
            && fight.WinnerId is null)
        {
            return;
        }

        fight.WhiteAthleteId = null;
        fight.BlueAthleteId = null;
        fight.WinnerId = null;
        fight.IsBye = false;
        fight.Status = Pending;
        fight.TatamiId = null;
        fight.WhiteScore = 0;
        fight.BlueScore = 0;
        fight.WhitePenalties = 0;
        fight.BluePenalties = 0;
        fight.WhiteIpponCount = 0;
        fight.WhiteWazaAriCount = 0;
        fight.WhiteYukoCount = 0;
        fight.BlueIpponCount = 0;
        fight.BlueWazaAriCount = 0;
        fight.BlueYukoCount = 0;
        fight.PausedAtUtc = null;
        fight.OsaeKomiSide = null;
        fight.OsaeKomiStartedAtUtc = null;
        fight.StartedAtUtc = null;
        fight.CompletedAtUtc = null;
        fight.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static void CompleteBye(FightRecord fight, Guid? winnerId)
    {
        fight.IsBye = true;
        fight.Status = Completed;
        fight.WinnerId = winnerId;
        fight.CompletedAtUtc = null;
        fight.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Returns the winner of a completed source fight, or null when the outcome is unknown.</summary>
    private static Guid? WinnerOf(FightRecord? source) =>
        source is { Status: var s, WinnerId: { } w } && s == Completed ? w : null;

    /// <summary>Returns the loser of a completed source fight, or null when not yet decided.</summary>
    private static Guid? LoserOf(FightRecord? source)
    {
        if (source is null || source.Status != Completed || source.WinnerId is null) return null;
        if (source.WhiteAthleteId is null || source.BlueAthleteId is null) return null;
        return source.WinnerId == source.WhiteAthleteId ? source.BlueAthleteId : source.WhiteAthleteId;
    }

    private Task BroadcastFightUpdatedAsync(FightRecord fight) =>
        _hub.Clients.Group(fight.TournamentId.ToString()).SendAsync(
            "FightUpdated",
            new FightUpdatedMessage(MapToFight(fight), DateTimeOffset.UtcNow),
            CancellationToken.None);

    private static Fight MapToFight(FightRecord r) => new(
        r.Id, r.TournamentId, r.CategoryId,
        Enum.Parse<FightBracketType>(r.BracketType),
        r.Round, r.FightNumber, r.PoolNumber,
        r.WhiteSourceFightId,
        r.WhiteSourceOutcome is null ? null : Enum.Parse<FightSlotSourceOutcome>(r.WhiteSourceOutcome),
        r.BlueSourceFightId,
        r.BlueSourceOutcome is null ? null : Enum.Parse<FightSlotSourceOutcome>(r.BlueSourceOutcome),
        r.WhiteAthleteId, r.BlueAthleteId, r.WinnerId,
        r.IsBye,
        Enum.Parse<FightStatus>(r.Status),
        r.TatamiId,
        r.WhiteScore, r.BlueScore, r.WhitePenalties, r.BluePenalties,
        r.WhiteIpponCount, r.WhiteWazaAriCount, r.WhiteYukoCount,
        r.BlueIpponCount, r.BlueWazaAriCount, r.BlueYukoCount,
        r.PausedAtUtc, r.OsaeKomiSide, r.OsaeKomiStartedAtUtc,
        r.StartedAtUtc, r.CompletedAtUtc,
        r.CreatedAtUtc, r.UpdatedAtUtc,
        IsGoldenScore: false);

    private static bool TryGetSide(string side, out bool whiteSide)
    {
        if (string.Equals(side, "white", StringComparison.OrdinalIgnoreCase))
        {
            whiteSide = true;
            return true;
        }

        if (string.Equals(side, "blue", StringComparison.OrdinalIgnoreCase))
        {
            whiteSide = false;
            return true;
        }

        whiteSide = default;
        return false;
    }

    private static MatchActionResult ApplyScoreDelta(FightRecord fight, bool whiteSide, ScoreType scoreType, int delta)
    {
        var targetIsWhite = whiteSide;
        switch (scoreType)
        {
            case ScoreType.Ippon:
                if (targetIsWhite)
                {
                    var newCount = fight.WhiteIpponCount + delta;
                    if (newCount < 0) return MatchActionResult.InvalidState;
                    fight.WhiteIpponCount = newCount;
                    fight.WhiteScore = ScoreValue(fight.WhiteIpponCount, fight.WhiteWazaAriCount, fight.WhiteYukoCount);
                }
                else
                {
                    var newCount = fight.BlueIpponCount + delta;
                    if (newCount < 0) return MatchActionResult.InvalidState;
                    fight.BlueIpponCount = newCount;
                    fight.BlueScore = ScoreValue(fight.BlueIpponCount, fight.BlueWazaAriCount, fight.BlueYukoCount);
                }
                return MatchActionResult.Success;
            case ScoreType.WazaAri:
                if (targetIsWhite)
                {
                    var newCount = fight.WhiteWazaAriCount + delta;
                    if (newCount < 0) return MatchActionResult.InvalidState;
                    fight.WhiteWazaAriCount = newCount;
                    fight.WhiteScore = ScoreValue(fight.WhiteIpponCount, fight.WhiteWazaAriCount, fight.WhiteYukoCount);
                }
                else
                {
                    var newCount = fight.BlueWazaAriCount + delta;
                    if (newCount < 0) return MatchActionResult.InvalidState;
                    fight.BlueWazaAriCount = newCount;
                    fight.BlueScore = ScoreValue(fight.BlueIpponCount, fight.BlueWazaAriCount, fight.BlueYukoCount);
                }
                return MatchActionResult.Success;
            case ScoreType.Yuko:
                if (targetIsWhite)
                {
                    var newCount = fight.WhiteYukoCount + delta;
                    if (newCount < 0) return MatchActionResult.InvalidState;
                    fight.WhiteYukoCount = newCount;
                    fight.WhiteScore = ScoreValue(fight.WhiteIpponCount, fight.WhiteWazaAriCount, fight.WhiteYukoCount);
                }
                else
                {
                    var newCount = fight.BlueYukoCount + delta;
                    if (newCount < 0) return MatchActionResult.InvalidState;
                    fight.BlueYukoCount = newCount;
                    fight.BlueScore = ScoreValue(fight.BlueIpponCount, fight.BlueWazaAriCount, fight.BlueYukoCount);
                }
                return MatchActionResult.Success;
            case ScoreType.Shido:
                if (targetIsWhite)
                {
                    var newCount = fight.WhitePenalties + delta;
                    if (newCount < 0) return MatchActionResult.InvalidState;
                    fight.WhitePenalties = newCount;
                }
                else
                {
                    var newCount = fight.BluePenalties + delta;
                    if (newCount < 0) return MatchActionResult.InvalidState;
                    fight.BluePenalties = newCount;
                }
                return MatchActionResult.Success;
            default:
                return MatchActionResult.InvalidState;
        }
    }

    private static int ScoreValue(int ipponCount, int wazaAriCount, int yukoCount) =>
        (ipponCount * 10) + (wazaAriCount * 7) + yukoCount;

    /// <summary>
    /// Checks whether all group-stage fights for a RoundRobinWithKnockout category are done
    /// and, if so, triggers the knockout bracket generation.
    /// </summary>
    private async Task TryAutoGenerateKnockoutAsync(
        Guid categoryId,
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

        if (category?.DrawFormat != BracketFormat.RoundRobinWithKnockout.ToString())
            return;

        var groupFights = await _dbContext.Fights
            .AsNoTracking()
            .Where(f => f.CategoryId == categoryId && f.BracketType == GroupStageType && !f.IsBye)
            .ToListAsync(cancellationToken);

        if (groupFights.Count == 0) return;

        bool allDone = groupFights.All(f => f.Status == Completed);
        if (!allDone) return;

        // Check no knockout fights already exist
        bool knockoutExists = await _dbContext.Fights
            .AnyAsync(f => f.CategoryId == categoryId && f.BracketType == MainType, cancellationToken);
        if (knockoutExists) return;

        var standings = await _rankingService.GetRoundRobinStandingsAsync(
            tournamentId, categoryId, cancellationToken);

        if (standings.Count == 0) return;

        var rankedAthletes = standings
            .Select(s => (s.AthleteId, Pool: s.PoolNumber))
            .ToList();

        var generated = await _bracketService.TryGenerateKnockoutFromGroupStageAsync(
            categoryId, rankedAthletes, cancellationToken);

        if (generated)
        {
            _logger.LogInformation(
                "Knockout bracket auto-generated for category {CategoryId} after group stage completion.",
                categoryId);

            _ = _hub.Clients.Group(tournamentId.ToString())
                .SendAsync("CategoryFightsUpdated",
                    new { tournamentId, categoryId },
                    CancellationToken.None);
        }
    }

}
