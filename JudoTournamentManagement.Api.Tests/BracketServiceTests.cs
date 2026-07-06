using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JudoTournamentManagement.Api.Tests;

public sealed class BracketServiceTests
{
    // ─── Infrastructure helpers ───────────────────────────────────────────────

    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "bracket.db");
    }

    private static AppDbContext CreateDbContext(string path)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}").Options;
        return new AppDbContext(opts);
    }

    private static BracketService CreateService(AppDbContext ctx) =>
        new(ctx, NullLogger<BracketService>.Instance);

    /// <summary>
    /// Seeds a complete tournament scenario: tournament + club + N athletes + 1 category
    /// with all athletes registered. Returns (tournamentId, categoryId, athleteIds).
    /// </summary>
    private static async Task<(Guid TournamentId, Guid CategoryId, List<Guid> AthleteIds)>
        SeedAsync(AppDbContext ctx, int athleteCount)
    {
        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t = await tStore.CreateAsync("T", new DateOnly(2026, 1, 1), "V", "O", CancellationToken.None);

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club = await clubStore.CreateAsync(t.Id, "JC Test", CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var athleteIds = new List<Guid>();
        for (int i = 0; i < athleteCount; i++)
        {
            var a = await athleteStore.CreateAsync(
                t.Id, club!.Id, $"A{i:D2}", "Tester", 2000 + i, Gender.Male, null, null, 1, true, CancellationToken.None);
            athleteIds.Add(a!.Id);
        }

        var catStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var cat = await catStore.CreateAsync(
            t.Id, "U18 M -73", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        var regStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        foreach (var aid in athleteIds)
        {
            var reg = await regStore.CreateAsync(t.Id, aid, 25.0m, null, false, CancellationToken.None);
            await regStore.AssignCategoryAsync(reg!.Id, cat!.Id, CancellationToken.None);
        }

        return (t.Id, cat!.Id, athleteIds);
    }

    // ─── Fight-count tests ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_WithTwoAthletes_CreatesOneFinalFight()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 2);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        Assert.Single(fights);
        Assert.Equal(FightBracketType.Main, fights[0].BracketType);
        Assert.Equal(FightStatus.Pending, fights[0].Status);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_WithFourAthletes_CreatesThreeFights()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 4);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        // bracketSize=4: 2 SF + 1 Final = 3
        Assert.Equal(3, fights.Count);
        Assert.Equal(2, fights.Count(f => f.Round == 1));
        Assert.Single(fights.Where(f => f.Round == 2));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_WithEightAthletes_CreatesSevenFights()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 8);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        // bracketSize=8: 4+2+1 = 7
        Assert.Equal(7, fights.Count);
        Assert.Equal(4, fights.Count(f => f.Round == 1));
        Assert.Equal(2, fights.Count(f => f.Round == 2));
        Assert.Single(fights.Where(f => f.Round == 3));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_WithSixteenAthletes_Creates15Fights()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 16);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        Assert.Equal(15, fights.Count);
    }

    // ─── Bye handling ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_WithFiveAthletes_HandlesByesWithoutByeVsByeFight()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 5);

        // bracketSize=8 → 7 fights, 3 R1 bye fights auto-completed
        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        Assert.Equal(7, fights.Count);

        var r1 = fights.Where(f => f.Round == 1).ToList();
        Assert.Equal(4, r1.Count);

        // No fight should have BOTH slots null (bye-vs-bye)
        Assert.DoesNotContain(r1, f => f.WhiteAthleteId is null && f.BlueAthleteId is null);

        // Bye fights are auto-completed with a winner
        var byeFights = r1.Where(f => f.IsBye).ToList();
        Assert.Equal(3, byeFights.Count);
        Assert.All(byeFights, f =>
        {
            Assert.Equal(FightStatus.Completed, f.Status);
            Assert.NotNull(f.WinnerId);
        });

        // One real fight remains pending
        Assert.Single(r1.Where(f => !f.IsBye && f.Status == FightStatus.Pending));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_ByeWinnerPropagatedIntoNextRoundSlot()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 5);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        // R2 fights should have some slots pre-filled from bye propagation
        var r2 = fights.Where(f => f.Round == 2).ToList();
        var preFilled = r2.Count(f => f.WhiteAthleteId.HasValue || f.BlueAthleteId.HasValue);
        Assert.True(preFilled > 0, "Bye winners should propagate into R2 slots.");
    }

    // ─── Repechage ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_WithRepechage_FourAthletes_AddsBronzeFight()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 4);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleEliminationWithRepechage, CancellationToken.None);

        // 3 main + 1 repechage = 4
        Assert.Equal(4, fights.Count);
        Assert.Single(fights.Where(f => f.BracketType == FightBracketType.Repechage));

        var bronze = fights.Single(f => f.BracketType == FightBracketType.Repechage);
        Assert.Equal(FightStatus.Pending, bronze.Status);
        Assert.Null(bronze.WhiteAthleteId);
        Assert.Null(bronze.BlueAthleteId);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_WithRepechage_EightAthletes_AddsBronzeFight()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 8);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleEliminationWithRepechage, CancellationToken.None);

        // 7 main + 1 repechage = 8
        Assert.Equal(8, fights.Count);
        Assert.Single(fights.Where(f => f.BracketType == FightBracketType.Repechage));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_WithRepechageTwoAthletes_NoRepechageFightAdded()
    {
        // With only 2 athletes there are no semi-finals → no 3rd place fight
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 2);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleEliminationWithRepechage, CancellationToken.None);

        Assert.Single(fights);
        Assert.DoesNotContain(fights, f => f.BracketType == FightBracketType.Repechage);
    }

    // ─── Determinism ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_CalledTwiceForSameCategory_ProducesSameBracket()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 8);

        var first = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        var second = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        // White/Blue assignments in round 1 must be identical
        var firstR1 = first.Where(f => f.Round == 1).OrderBy(f => f.FightNumber).ToList();
        var secondR1 = second.Where(f => f.Round == 1).OrderBy(f => f.FightNumber).ToList();

        Assert.Equal(firstR1.Count, secondR1.Count);
        for (int i = 0; i < firstR1.Count; i++)
        {
            Assert.Equal(firstR1[i].WhiteAthleteId, secondR1[i].WhiteAthleteId);
            Assert.Equal(firstR1[i].BlueAthleteId, secondR1[i].BlueAthleteId);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_ReplacesExistingFightsWhenCalledAgain()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 4);
        var svc = CreateService(ctx);

        await svc.GenerateAsync(tid, cid, BracketFormat.SingleElimination, CancellationToken.None);
        var second = await svc.GenerateAsync(tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        // Still exactly 3 fights (not 6)
        Assert.Equal(3, second.Count);
        Assert.Equal(3, await ctx.Fights.CountAsync(f => f.CategoryId == cid));
    }

    // ─── Category lock ────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Generate_KeepsCategoryUnlockedBeforeFirstFightStart()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 4);

        await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        var category = await ctx.Categories.FindAsync(cid);
        Assert.False(category!.IsLocked);
    }

    // ─── Minimum athletes guard ───────────────────────────────────────────────

    [Theory]
    [Trait("Category", "UnitTest")]
    [InlineData(BracketFormat.SingleElimination)]
    [InlineData(BracketFormat.SingleEliminationWithRepechage)]
    [InlineData(BracketFormat.RoundRobin)]
    [InlineData(BracketFormat.RoundRobinWithKnockout)]
    public async Task Generate_WithOneAthlete_CreatesSingleCompletedByeFight(BracketFormat format)
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, athleteIds) = await SeedAsync(ctx, 1);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, format, CancellationToken.None);

        var fight = Assert.Single(fights);
        Assert.Equal(FightBracketType.Main, fight.BracketType);
        Assert.Equal(1, fight.Round);
        Assert.Equal(1, fight.FightNumber);
        Assert.True(fight.IsBye);
        Assert.Equal(FightStatus.Completed, fight.Status);
        Assert.Equal(athleteIds[0], fight.WhiteAthleteId);
        Assert.Null(fight.BlueAthleteId);
        Assert.Equal(athleteIds[0], fight.WinnerId);
    }

    // ─── Swap tests ───────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task SwapAthletes_BeforeAnyFightStarted_SwapsSuccessfully()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, athletes) = await SeedAsync(ctx, 4);
        var svc = CreateService(ctx);
        await svc.GenerateAsync(tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        // Find the two R1 fight athletes
        var r1Fights = await ctx.Fights
            .Where(f => f.CategoryId == cid && f.Round == 1)
            .OrderBy(f => f.FightNumber)
            .ToListAsync();

        var a1 = r1Fights[0].WhiteAthleteId!.Value;
        var a2 = r1Fights[1].WhiteAthleteId!.Value; // from a different fight

        var result = await svc.SwapAthletesAsync(cid, a1, a2, CancellationToken.None);

        Assert.Equal(SwapResult.Success, result);

        // Verify swap took effect
        var updated = await ctx.Fights
            .Where(f => f.CategoryId == cid && f.Round == 1)
            .OrderBy(f => f.FightNumber)
            .ToListAsync();

        Assert.Equal(a2, updated[0].WhiteAthleteId);
        Assert.Equal(a1, updated[1].WhiteAthleteId);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task SwapAthletes_AfterFightStarted_ReturnsBracketLocked()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, athletes) = await SeedAsync(ctx, 4);
        var svc = CreateService(ctx);
        await svc.GenerateAsync(tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        // Manually start the first non-bye fight
        var realFight = await ctx.Fights
            .FirstAsync(f => f.CategoryId == cid && !f.IsBye);
        realFight.Status = FightStatus.InProgress.ToString();
        await ctx.SaveChangesAsync();

        var result = await svc.SwapAthletesAsync(
            cid, athletes[0], athletes[1], CancellationToken.None);

        Assert.Equal(SwapResult.BracketLocked, result);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task SwapAthletes_WhenAthleteNotInBracket_ReturnsAthleteNotInBracket()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, athletes) = await SeedAsync(ctx, 4);
        var svc = CreateService(ctx);
        await svc.GenerateAsync(tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        var result = await svc.SwapAthletesAsync(
            cid, athletes[0], Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(SwapResult.AthleteNotInBracket, result);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task SwapAthletes_ByeFightWinnerAlsoUpdated()
    {
        // With 5 athletes: 3 bye fights, winners are pre-propagated.
        // Swapping a bye-fight winner should update WinnerId in the bye fight
        // AND all downstream propagated slots consistently.
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 5);
        var svc = CreateService(ctx);

        var fights = await svc.GenerateAsync(
            tid, cid, BracketFormat.SingleElimination, CancellationToken.None);

        // Pick the first bye fight and its auto-advanced winner
        var byeFight = fights.First(f => f.IsBye);
        var autoAdvanced = byeFight.WinnerId!.Value;
        var otherFight = fights.First(f => f.Round == 1 && !f.IsBye);
        var other = otherFight.WhiteAthleteId!.Value; // athlete from the one real fight

        var result = await svc.SwapAthletesAsync(cid, autoAdvanced, other, CancellationToken.None);

        Assert.Equal(SwapResult.Success, result);

        // After swap: the bye fight's winner should now be 'other'
        var updatedFights = await ctx.Fights.Where(f => f.CategoryId == cid).ToListAsync();
        var updatedByeFight = updatedFights.First(f => f.Id == byeFight.Id);
        Assert.Equal(other, updatedByeFight.WinnerId);

        // 'autoAdvanced' should now be in the slot where 'other' previously was
        var updatedRealFight = updatedFights.First(f => f.Id == otherFight.Id);
        Assert.Equal(autoAdvanced, updatedRealFight.WhiteAthleteId);
    }

    // ─── Round-robin tests ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GenerateRoundRobin_FourAthletes_Creates6Fights()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 4);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.RoundRobin, CancellationToken.None);

        // N*(N-1)/2 = 4*3/2 = 6 real fights
        var real = fights.Where(f => !f.IsBye).ToList();
        Assert.Equal(6, real.Count);
        Assert.All(real, f => Assert.Equal(FightBracketType.Main, f.BracketType));
        Assert.All(real, f => Assert.Null(f.PoolNumber));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GenerateRoundRobin_FiveAthletes_Creates10RealFights()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 5);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.RoundRobin, CancellationToken.None);

        // N*(N-1)/2 = 5*4/2 = 10 real fights
        var real = fights.Where(f => !f.IsBye).ToList();
        Assert.Equal(10, real.Count);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GenerateRoundRobin_EachAthleteAppearsInCorrectNumberOfFights()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, athleteIds) = await SeedAsync(ctx, 4);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.RoundRobin, CancellationToken.None);

        // Each of 4 athletes should appear in exactly 3 fights (vs every other)
        foreach (var athleteId in athleteIds)
        {
            int count = fights.Count(f =>
                !f.IsBye && (f.WhiteAthleteId == athleteId || f.BlueAthleteId == athleteId));
            Assert.Equal(3, count);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GenerateRoundRobin_NoDuplicatePairings()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 6);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.RoundRobin, CancellationToken.None);

        var real = fights.Where(f => !f.IsBye).ToList();
        var pairs = real
            .Select(f => (
                A: f.WhiteAthleteId < f.BlueAthleteId ? f.WhiteAthleteId : f.BlueAthleteId,
                B: f.WhiteAthleteId < f.BlueAthleteId ? f.BlueAthleteId : f.WhiteAthleteId))
            .ToList();

        // All pairings must be unique
        Assert.Equal(pairs.Count, pairs.Distinct().Count());
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GenerateRoundRobin_PersistsDrawFormatOnCategory()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 3);

        await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.RoundRobin, CancellationToken.None);

        var categoryRecord = await ctx.Categories.FirstAsync(c => c.Id == cid);
        Assert.Equal(BracketFormat.RoundRobin.ToString(), categoryRecord.DrawFormat);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GenerateRoundRobinWithKnockout_SixAthletes_CreatesTwoPoolsOf3()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 6);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.RoundRobinWithKnockout, CancellationToken.None);

        var groupFights = fights.Where(f => f.BracketType == FightBracketType.GroupStage).ToList();
        var pool1 = groupFights.Where(f => f.PoolNumber == 1).ToList();
        var pool2 = groupFights.Where(f => f.PoolNumber == 2).ToList();

        // 6 athletes → 3 per pool → 3*(3-1)/2 = 3 fights per pool
        Assert.Equal(3, pool1.Where(f => !f.IsBye).Count());
        Assert.Equal(3, pool2.Where(f => !f.IsBye).Count());
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GenerateRoundRobinWithKnockout_NoKnockoutFightsGeneratedInitially()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid, _) = await SeedAsync(ctx, 4);

        var fights = await CreateService(ctx).GenerateAsync(
            tid, cid, BracketFormat.RoundRobinWithKnockout, CancellationToken.None);

        // No Main or Repechage fights yet — only GroupStage
        Assert.DoesNotContain(fights, f => f.BracketType == FightBracketType.Main);
        Assert.DoesNotContain(fights, f => f.BracketType == FightBracketType.Repechage);
    }
}
