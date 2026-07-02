using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Generates and manages fight brackets for judo categories.
/// </summary>
public sealed class BracketService : IBracketService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<BracketService> _logger;

    /// <summary>Initializes a new service instance.</summary>
    public BracketService(AppDbContext dbContext, ILogger<BracketService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Fight>> GenerateAsync(
        Guid tournamentId,
        Guid categoryId,
        BracketFormat format,
        CancellationToken cancellationToken)
    {
        // Check for unassigned athletes
        var unassignedCount = await _dbContext.Registrations
            .AsNoTracking()
            .CountAsync(r => r.TournamentId == tournamentId && r.CategoryId == null, cancellationToken);

        if (unassignedCount > 0)
        {
            throw new InvalidOperationException(
                $"Auslosung nicht möglich: {unassignedCount} Athleten ohne zugewiesene Kategorie.");
        }

        // Retrieve registered athletes ordered deterministically before shuffling
        var registrations = await _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.CategoryId == categoryId)
            .Include(r => r.Athlete)
            .OrderBy(r => r.Athlete!.LastName)
            .ThenBy(r => r.Athlete!.FirstName)
            .ThenBy(r => r.AthleteId)
            .ToListAsync(cancellationToken);

        if (registrations.Count < 2)
            throw new InvalidOperationException(
                "Mindestens 2 Athleten sind für die Auslosung erforderlich.");

        // Deterministic shuffle: seed derived from category ID so same input → same bracket
        var athletes = registrations.Select(r => r.AthleteId).ToList();
        var seed = Math.Abs(categoryId.GetHashCode());
        var rng = new Random(seed);
        var shuffled = athletes.OrderBy(_ => rng.NextDouble()).ToList();

        List<FightRecord> fights;
        var utcNow = DateTimeOffset.UtcNow;

        if (format == BracketFormat.RoundRobin)
        {
            fights = GenerateRoundRobinFights(
                tournamentId, categoryId, shuffled,
                FightBracketType.Main, poolNumber: null, utcNow);
        }
        else if (format == BracketFormat.RoundRobinWithKnockout)
        {
            // Split athletes deterministically into 2 pools
            int pool1Size = (shuffled.Count + 1) / 2;
            var pool1 = shuffled.Take(pool1Size).ToList();
            var pool2 = shuffled.Skip(pool1Size).ToList();

            fights = GenerateRoundRobinFights(
                tournamentId, categoryId, pool1,
                FightBracketType.GroupStage, poolNumber: 1, utcNow);
            fights.AddRange(GenerateRoundRobinFights(
                tournamentId, categoryId, pool2,
                FightBracketType.GroupStage, poolNumber: 2, utcNow));
        }
        else
        {
            // Single-elimination (with optional repechage)
            var bracketSize = NextPowerOfTwo(shuffled.Count);
            var numRounds = (int)Math.Log2(bracketSize);
            var slots = CreateSlots(shuffled, bracketSize);

            fights = new List<FightRecord>();

            // ── Round 1: assign athletes from slots ──────────────────────────────
            for (int i = 0; i < bracketSize / 2; i++)
            {
                var white = slots[i * 2];
                var blue = slots[i * 2 + 1];
                var isBye = white is null || blue is null;

                fights.Add(new FightRecord
                {
                    Id = Guid.NewGuid(),
                    TournamentId = tournamentId,
                    CategoryId = categoryId,
                    BracketType = FightBracketType.Main.ToString(),
                    Round = 1,
                    FightNumber = i + 1,
                    WhiteAthleteId = white,
                    BlueAthleteId = blue,
                    WinnerId = isBye ? (white ?? blue) : null,
                    IsBye = isBye,
                    Status = isBye
                        ? FightStatus.Completed.ToString()
                        : FightStatus.Pending.ToString(),
                    CreatedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                });
            }

            // ── Remaining rounds: TBD slots ──────────────────────────────────────
            for (int round = 2; round <= numRounds; round++)
            {
                int numFightsInRound = bracketSize / (1 << round);
                for (int i = 0; i < numFightsInRound; i++)
                {
                    fights.Add(new FightRecord
                    {
                        Id = Guid.NewGuid(),
                        TournamentId = tournamentId,
                        CategoryId = categoryId,
                        BracketType = FightBracketType.Main.ToString(),
                        Round = round,
                        FightNumber = i + 1,
                        Status = FightStatus.Pending.ToString(),
                        CreatedAtUtc = utcNow,
                        UpdatedAtUtc = utcNow
                    });
                }
            }

            // ── Propagate bye winners so later-round slots get pre-filled ────────
            PropagateByeWinners(fights);

            // ── Optional: 3rd-place consolation fight (repechage) ────────────────
            if (format == BracketFormat.SingleEliminationWithRepechage && bracketSize >= 4)
            {
                fights.Add(new FightRecord
                {
                    Id = Guid.NewGuid(),
                    TournamentId = tournamentId,
                    CategoryId = categoryId,
                    BracketType = FightBracketType.Repechage.ToString(),
                    Round = numRounds,
                    FightNumber = 1,
                    Status = FightStatus.Pending.ToString(),
                    CreatedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                });
            }
        }

        // ── Replace any existing bracket ─────────────────────────────────────
        var existing = await _dbContext.Fights
            .Where(f => f.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            _dbContext.Fights.RemoveRange(existing);

        // ── Persist the draw format. Locking happens when the first real fight starts. ──
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        if (category is not null)
        {
            category.DrawFormat = format.ToString();
        }

        _dbContext.Fights.AddRange(fights);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bracket generated for category {CategoryId}: {Count} fights ({Format}).",
            categoryId, fights.Count, format);

        return fights.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<SwapResult> SwapAthletesAsync(
        Guid categoryId,
        Guid athleteId1,
        Guid athleteId2,
        CancellationToken cancellationToken)
    {
        var fights = await _dbContext.Fights
            .Where(f => f.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        // Bracket is locked once any real (non-bye) fight has been started or completed
        var locked = fights.Any(f =>
            !f.IsBye && f.Status != FightStatus.Pending.ToString());

        if (locked)
        {
            _logger.LogWarning("Swap blocked: bracket for category {CategoryId} is locked.", categoryId);
            return SwapResult.BracketLocked;
        }

        bool found1 = fights.Any(f =>
            f.WhiteAthleteId == athleteId1
            || f.BlueAthleteId == athleteId1
            || f.WinnerId == athleteId1);

        bool found2 = fights.Any(f =>
            f.WhiteAthleteId == athleteId2
            || f.BlueAthleteId == athleteId2
            || f.WinnerId == athleteId2);

        if (!found1 || !found2)
        {
            _logger.LogWarning(
                "Swap failed: athlete(s) not found in bracket for category {CategoryId}.",
                categoryId);
            return SwapResult.AthleteNotInBracket;
        }

        // Two-pass swap using a sentinel to prevent id collision during the replacement
        var sentinel = Guid.NewGuid();

        foreach (var f in fights)
        {
            if (f.WhiteAthleteId == athleteId1) f.WhiteAthleteId = sentinel;
            else if (f.WhiteAthleteId == athleteId2) f.WhiteAthleteId = athleteId1;

            if (f.BlueAthleteId == athleteId1) f.BlueAthleteId = sentinel;
            else if (f.BlueAthleteId == athleteId2) f.BlueAthleteId = athleteId1;

            if (f.WinnerId == athleteId1) f.WinnerId = sentinel;
            else if (f.WinnerId == athleteId2) f.WinnerId = athleteId1;
        }

        foreach (var f in fights)
        {
            if (f.WhiteAthleteId == sentinel) f.WhiteAthleteId = athleteId2;
            if (f.BlueAthleteId == sentinel) f.BlueAthleteId = athleteId2;
            if (f.WinnerId == sentinel) f.WinnerId = athleteId2;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Athletes {A1} and {A2} swapped in category {CategoryId} bracket.",
            athleteId1, athleteId2, categoryId);
        return SwapResult.Success;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Generates all round-robin fights for a set of athletes using the circle scheduling algorithm.
    /// Each athlete fights every other athlete exactly once.
    /// If <paramref name="athletes"/> count is odd, one bye fight is added per round.
    /// </summary>
    private static List<FightRecord> GenerateRoundRobinFights(
        Guid tournamentId,
        Guid categoryId,
        List<Guid> athletes,
        FightBracketType bracketType,
        int? poolNumber,
        DateTimeOffset utcNow)
    {
        var fights = new List<FightRecord>();
        if (athletes.Count < 2) return fights;

        // Circle scheduling: add a null "bye" placeholder if count is odd
        var participants = athletes.Cast<Guid?>().ToList();
        if (participants.Count % 2 == 1)
            participants.Add(null);

        int n = participants.Count;
        int rounds = n - 1;
        int fightNumberGlobal = 0;

        for (int round = 0; round < rounds; round++)
        {
            int fightInRound = 0;
            for (int i = 0; i < n / 2; i++)
            {
                int j = n - 1 - i;
                var white = participants[i];
                var blue = participants[j];
                bool isBye = white is null || blue is null;

                fightNumberGlobal++;
                fightInRound++;

                fights.Add(new FightRecord
                {
                    Id = Guid.NewGuid(),
                    TournamentId = tournamentId,
                    CategoryId = categoryId,
                    BracketType = bracketType.ToString(),
                    Round = round + 1,
                    FightNumber = fightInRound,
                    PoolNumber = poolNumber,
                    WhiteAthleteId = white,
                    BlueAthleteId = blue,
                    WinnerId = isBye ? (white ?? blue) : null,
                    IsBye = isBye,
                    Status = isBye
                        ? FightStatus.Completed.ToString()
                        : FightStatus.Pending.ToString(),
                    CreatedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                });
            }

            // Rotate: keep participants[0] fixed, rotate the rest clockwise
            var last = participants[n - 1];
            participants.RemoveAt(n - 1);
            participants.Insert(1, last);
        }

        return fights;
    }

    /// <summary>
    /// Generates the knockout bracket from the group-stage results of a
    /// <see cref="BracketFormat.RoundRobinWithKnockout"/> category.
    /// Called automatically after all group-stage fights are completed.
    /// </summary>
    public async Task<bool> TryGenerateKnockoutFromGroupStageAsync(
        Guid categoryId,
        IReadOnlyList<(Guid AthleteId, int Pool)> rankedAthletes,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTimeOffset.UtcNow;

        // Expect top 2 from each of 2 pools: [Pool1 rank1, Pool1 rank2, Pool2 rank1, Pool2 rank2]
        var pool1 = rankedAthletes.Where(a => a.Pool == 1).Take(2).ToList();
        var pool2 = rankedAthletes.Where(a => a.Pool == 2).Take(2).ToList();

        if (pool1.Count < 1 || pool2.Count < 1) return false;

        // Remove any existing knockout fights (keep group stage fights)
        var existingKnockout = await _dbContext.Fights
            .Where(f => f.CategoryId == categoryId
                && (f.BracketType == FightBracketType.Main.ToString()
                    || f.BracketType == FightBracketType.Repechage.ToString()))
            .ToListAsync(cancellationToken);

        if (existingKnockout.Count > 0)
            _dbContext.Fights.RemoveRange(existingKnockout);

        var fights = new List<FightRecord>();

        // Get the tournament ID from an existing group-stage fight
        var groupFight = await _dbContext.Fights
            .AsNoTracking()
            .Where(f => f.CategoryId == categoryId && f.BracketType == FightBracketType.GroupStage.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        if (groupFight is null) return false;
        var tournamentId = groupFight.TournamentId;

        // Semifinal 1: Pool1 1st vs Pool2 2nd
        Guid? sf1White = pool1[0].AthleteId;
        Guid? sf1Blue = pool2.Count >= 2 ? pool2[1].AthleteId : null;

        // Semifinal 2: Pool2 1st vs Pool1 2nd
        Guid? sf2White = pool2[0].AthleteId;
        Guid? sf2Blue = pool1.Count >= 2 ? pool1[1].AthleteId : null;

        bool sf1Bye = sf1Blue is null;
        bool sf2Bye = sf2Blue is null;

        fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            CategoryId = categoryId,
            BracketType = FightBracketType.Main.ToString(),
            Round = 1,
            FightNumber = 1,
            WhiteAthleteId = sf1White,
            BlueAthleteId = sf1Blue,
            WinnerId = sf1Bye ? sf1White : null,
            IsBye = sf1Bye,
            Status = sf1Bye ? FightStatus.Completed.ToString() : FightStatus.Pending.ToString(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            CategoryId = categoryId,
            BracketType = FightBracketType.Main.ToString(),
            Round = 1,
            FightNumber = 2,
            WhiteAthleteId = sf2White,
            BlueAthleteId = sf2Blue,
            WinnerId = sf2Bye ? sf2White : null,
            IsBye = sf2Bye,
            Status = sf2Bye ? FightStatus.Completed.ToString() : FightStatus.Pending.ToString(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        // Final (Round 2, Fight 1)
        fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            CategoryId = categoryId,
            BracketType = FightBracketType.Main.ToString(),
            Round = 2,
            FightNumber = 1,
            Status = FightStatus.Pending.ToString(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        // 3rd-place fight (Repechage Round 2)
        fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            CategoryId = categoryId,
            BracketType = FightBracketType.Repechage.ToString(),
            Round = 2,
            FightNumber = 1,
            Status = FightStatus.Pending.ToString(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        // Propagate bye winners from semis into final / 3rd-place
        PropagateByeWinners(fights);

        _dbContext.Fights.AddRange(fights);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Knockout bracket generated for RoundRobinWithKnockout category {CategoryId}: {Count} fights.",
            categoryId, fights.Count);

        return true;
    }


    private static void PropagateByeWinners(List<FightRecord> fights)
    {
        int maxRound = fights.Max(f => f.Round);
        var mainType = FightBracketType.Main.ToString();

        for (int round = 1; round < maxRound; round++)
        {
            var completed = fights
                .Where(f => f.Round == round && f.BracketType == mainType && f.WinnerId.HasValue)
                .OrderBy(f => f.FightNumber)
                .ToList();

            foreach (var fight in completed)
            {
                int nextFightNumber = (fight.FightNumber + 1) / 2;
                bool isWhiteSlot = fight.FightNumber % 2 == 1;

                var next = fights.FirstOrDefault(f =>
                    f.Round == round + 1
                    && f.FightNumber == nextFightNumber
                    && f.BracketType == mainType);

                if (next is null) continue;

                if (isWhiteSlot) next.WhiteAthleteId = fight.WinnerId;
                else next.BlueAthleteId = fight.WinnerId;
            }
        }
    }

    /// <summary>
    /// Distributes <paramref name="athletes"/> across <paramref name="bracketSize"/> slots,
    /// spreading byes (null) evenly so no fight has two null opponents.
    /// </summary>
    private static Guid?[] CreateSlots(List<Guid> athletes, int bracketSize)
    {
        int numFights = bracketSize / 2;
        int byes = bracketSize - athletes.Count;
        int fullFights = numFights - byes; // fights with two real athletes

        var slots = new Guid?[bracketSize];
        int idx = 0;

        // Complete fights first
        for (int i = 0; i < fullFights; i++)
        {
            slots[i * 2] = athletes[idx++];
            slots[i * 2 + 1] = athletes[idx++];
        }

        // Bye fights: one athlete + null
        for (int i = fullFights; i < numFights; i++)
        {
            slots[i * 2] = athletes[idx++];
            slots[i * 2 + 1] = null;
        }

        return slots;
    }

    /// <summary>Returns the smallest power of two that is ≥ <paramref name="n"/>.</summary>
    private static int NextPowerOfTwo(int n)
    {
        if (n <= 1) return 1;
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    private static Fight MapToModel(FightRecord r) =>
        new(r.Id,
            r.TournamentId,
            r.CategoryId,
            Enum.Parse<FightBracketType>(r.BracketType),
            r.Round,
            r.FightNumber,
            r.PoolNumber,
            r.WhiteAthleteId,
            r.BlueAthleteId,
            r.WinnerId,
            r.IsBye,
            Enum.Parse<FightStatus>(r.Status),
            r.TatamiId,
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
            IsGoldenScore: false);
}
