using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Hubs;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

/// <summary>
/// Unit tests for <see cref="TatamiQueueService"/> covering Current / Next / On-deck ordering,
/// playability filtering and manual reassignment (F-01).
/// </summary>
public sealed class TatamiQueueServiceTests
{
    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "queue.db");
    }

    private static AppDbContext CreateDbContext(string path)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}").Options;
        return new AppDbContext(opts);
    }

    private static MatchService CreateMatchService(AppDbContext ctx)
    {
        var mockHub = new Mock<IHubContext<TournamentHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockProxy = new Mock<IClientProxy>();
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockProxy.Object);
        mockProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new MatchService(ctx, new AuditLogService(ctx, NullLogger<AuditLogService>.Instance), mockHub.Object, new Mock<IBracketService>().Object, new Mock<IRankingService>().Object, NullLogger<MatchService>.Instance);
    }

    private static async Task<(Guid TournamentId, Guid CategoryId, Guid TatamiId)>
        SeedAsync(AppDbContext ctx, int athleteCount)
    {
        var t = await new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance)
            .CreateAsync("T", new DateOnly(2026, 1, 1), "V", "O", CancellationToken.None);

        var club = await new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance)
            .CreateAsync(t.Id, "JC Test", CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        for (int i = 0; i < athleteCount; i++)
        {
            await athleteStore.CreateAsync(
                t.Id, club!.Id, $"A{i:D2}", "Tester", 2000 + i, Gender.Male, null, null, true, CancellationToken.None);
        }

        var cat = await new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance)
            .CreateAsync(t.Id, "U18 M -73", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        var regStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var athletes = await ctx.Athletes.AsNoTracking().Select(a => a.Id).ToListAsync();
        foreach (var aid in athletes)
        {
            var reg = await regStore.CreateAsync(t.Id, aid, 25.0m, null, false, CancellationToken.None);
            await regStore.AssignCategoryAsync(reg!.Id, cat!.Id, CancellationToken.None);
        }

        await new BracketService(ctx, NullLogger<BracketService>.Instance)
            .GenerateAsync(t.Id, cat!.Id, BracketFormat.SingleElimination, CancellationToken.None);

        var tatami = await new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance)
            .CreateAsync(t.Id, "Tatami 1", 0, CancellationToken.None);

        return (t.Id, cat.Id, tatami.Id);
    }

    private static async Task<List<FightRecord>> RoundOneFightsAsync(string db, Guid categoryId)
    {
        await using var ctx = CreateDbContext(db);
        return await ctx.Fights.AsNoTracking()
            .Where(f => f.CategoryId == categoryId && f.Round == 1
                        && f.BracketType == FightBracketType.Main.ToString())
            .OrderBy(f => f.FightNumber)
            .ToListAsync();
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetQueue_UnknownTatami_ReturnsNull()
    {
        var db = CreateDatabasePath();
        Guid tid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, _, _) = await SeedAsync(ctx, 4);
        }

        await using var ctx2 = CreateDbContext(db);
        var queue = await new TatamiQueueService(ctx2).GetQueueAsync(tid, Guid.NewGuid(), CancellationToken.None);
        Assert.Null(queue);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetQueue_OrdersAssignedReadyFights_AsCurrentNextOnDeck()
    {
        var db = CreateDatabasePath();
        Guid tid, cid, tatamiId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, tatamiId) = await SeedAsync(ctx, 8); // 4 round-1 fights, all real
        }

        var roundOne = await RoundOneFightsAsync(db, cid);

        await using (var ctx = CreateDbContext(db))
        {
            var match = CreateMatchService(ctx);
            foreach (var f in roundOne)
                await match.AssignTatamiAsync(f.Id, tatamiId, "Admin", CancellationToken.None);
        }

        await using var ctx2 = CreateDbContext(db);
        var queue = await new TatamiQueueService(ctx2).GetQueueAsync(tid, tatamiId, CancellationToken.None);

        Assert.NotNull(queue);
        Assert.Equal(roundOne[0].Id, queue!.Current!.Id);
        Assert.Equal(roundOne[1].Id, queue.Next!.Id);
        Assert.Equal(roundOne[2].Id, queue.OnDeck!.Id);
        Assert.Equal(4, queue.Upcoming.Count);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetQueue_InProgressFight_BecomesCurrent()
    {
        var db = CreateDatabasePath();
        Guid tid, cid, tatamiId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, tatamiId) = await SeedAsync(ctx, 8);
        }

        var roundOne = await RoundOneFightsAsync(db, cid);

        await using (var ctx = CreateDbContext(db))
        {
            var match = CreateMatchService(ctx);
            foreach (var f in roundOne)
                await match.AssignTatamiAsync(f.Id, tatamiId, "Admin", CancellationToken.None);
            // Start the second fight: it must surface as Current ahead of the pending first.
            await match.StartAsync(roundOne[1].Id, "T1", CancellationToken.None);
        }

        await using var ctx2 = CreateDbContext(db);
        var queue = await new TatamiQueueService(ctx2).GetQueueAsync(tid, tatamiId, CancellationToken.None);

        Assert.Equal(roundOne[1].Id, queue!.Current!.Id);
        Assert.Equal(FightStatus.InProgress, queue.Current.Status);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetQueue_ExcludesFightsWithUnassignedSlots()
    {
        var db = CreateDatabasePath();
        Guid tid, cid, tatamiId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, tatamiId) = await SeedAsync(ctx, 4); // final (round 2) has TBD slots
        }

        Guid finalId;
        await using (var ctx = CreateDbContext(db))
        {
            finalId = await ctx.Fights
                .Where(f => f.CategoryId == cid && f.Round == 2)
                .Select(f => f.Id).SingleAsync();
        }

        await using (var ctx = CreateDbContext(db))
        {
            await CreateMatchService(ctx).AssignTatamiAsync(finalId, tatamiId, "Admin", CancellationToken.None);
        }

        await using var ctx2 = CreateDbContext(db);
        var queue = await new TatamiQueueService(ctx2).GetQueueAsync(tid, tatamiId, CancellationToken.None);

        // Final still has TBD athletes -> not playable -> queue empty.
        Assert.Null(queue!.Current);
        Assert.Empty(queue.Upcoming);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Reassignment_RemovesFightFromPreviousTatamiQueue()
    {
        var db = CreateDatabasePath();
        Guid tid, cid, tatami1;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, tatami1) = await SeedAsync(ctx, 4);
        }

        Guid tatami2;
        await using (var ctx = CreateDbContext(db))
        {
            var t2 = await new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance)
                .CreateAsync(tid, "Tatami 2", 1, CancellationToken.None);
            tatami2 = t2.Id;
        }

        var fight = (await RoundOneFightsAsync(db, cid))[0];

        await using (var ctx = CreateDbContext(db))
        {
            var match = CreateMatchService(ctx);
            await match.AssignTatamiAsync(fight.Id, tatami1, "Admin", CancellationToken.None);
            await match.AssignTatamiAsync(fight.Id, tatami2, "Admin", CancellationToken.None);
        }

        await using var ctx2 = CreateDbContext(db);
        var queueService = new TatamiQueueService(ctx2);
        var queue1 = await queueService.GetQueueAsync(tid, tatami1, CancellationToken.None);
        var queue2 = await queueService.GetQueueAsync(tid, tatami2, CancellationToken.None);

        Assert.Empty(queue1!.Upcoming);
        Assert.Contains(queue2!.Upcoming, f => f.Id == fight.Id);
    }

    /// <summary>
    /// When a fight is in-progress, golden score is enabled, elapsed time exceeds the regular match
    /// duration, and the fighters have tied scores (equal waza-ari and yuko), the queue entry
    /// should report <c>IsGoldenScore = true</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetQueue_InProgressFightExceedsMatchDuration_TiedScores_IsGoldenScoreTrue()
    {
        var db = CreateDatabasePath();
        Guid tid, cid, tatamiId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, tatamiId) = await SeedAsync(ctx, 4);
        }

        // Enable golden score for the category (match duration = 300 s).
        await using (var ctx = CreateDbContext(db))
        {
            var cat = await ctx.Categories.FirstAsync(c => c.Id == cid);
            cat.GoldenScoreEnabled = true;
            cat.GoldenScoreDurationSeconds = 180;
            await ctx.SaveChangesAsync();
        }

        var roundOne = await RoundOneFightsAsync(db, cid);
        var fightId = roundOne[0].Id;

        await using (var ctx = CreateDbContext(db))
        {
            var match = CreateMatchService(ctx);
            await match.AssignTatamiAsync(fightId, tatamiId, "Admin", CancellationToken.None);
            await match.StartAsync(fightId, "T1", CancellationToken.None);
        }

        // Set tied scores: both fighters with 1 waza-ari and 1 yuko.
        await using (var ctx = CreateDbContext(db))
        {
            var record = await ctx.Fights.FirstAsync(f => f.Id == fightId);
            record.WhiteWazaAriCount = 1;
            record.BlueWazaAriCount = 1;
            record.WhiteYukoCount = 1;
            record.BlueYukoCount = 1;
            // Backdate StartedAtUtc so elapsed time exceeds regular match duration (300 s).
            record.StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-(300 + 30)); // 30 s into GS
            await ctx.SaveChangesAsync();
        }

        await using var ctx2 = CreateDbContext(db);
        var queue = await new TatamiQueueService(ctx2).GetQueueAsync(tid, tatamiId, CancellationToken.None);

        Assert.NotNull(queue?.Current);
        Assert.Equal(fightId, queue!.Current!.Id);
        Assert.True(queue.Current.IsGoldenScore, "Fight with tied scores past match duration should be in golden-score phase.");
    }

    /// <summary>
    /// When a fight is in-progress, golden score is enabled, elapsed time exceeds the regular
    /// match duration, but one fighter has a higher score (leading), the queue entry should
    /// report <c>IsGoldenScore = false</c> (no golden score when winner is determined).
    /// </summary>
    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetQueue_InProgressFightExceedsMatchDuration_LeadingScore_IsGoldenScoreFalse()
    {
        var db = CreateDatabasePath();
        Guid tid, cid, tatamiId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, tatamiId) = await SeedAsync(ctx, 4);
        }

        // Enable golden score for the category.
        await using (var ctx = CreateDbContext(db))
        {
            var cat = await ctx.Categories.FirstAsync(c => c.Id == cid);
            cat.GoldenScoreEnabled = true;
            cat.GoldenScoreDurationSeconds = 180;
            await ctx.SaveChangesAsync();
        }

        var roundOne = await RoundOneFightsAsync(db, cid);
        var fightId = roundOne[0].Id;

        await using (var ctx = CreateDbContext(db))
        {
            var match = CreateMatchService(ctx);
            await match.AssignTatamiAsync(fightId, tatamiId, "Admin", CancellationToken.None);
            await match.StartAsync(fightId, "T1", CancellationToken.None);
        }

        // Set leading score: white has 1 waza-ari, blue has 0 (white leads).
        await using (var ctx = CreateDbContext(db))
        {
            var record = await ctx.Fights.FirstAsync(f => f.Id == fightId);
            record.WhiteWazaAriCount = 1;
            record.BlueWazaAriCount = 0;
            record.WhiteYukoCount = 1;
            record.BlueYukoCount = 1;
            // Backdate StartedAtUtc so elapsed time exceeds regular match duration.
            record.StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-(300 + 30));
            await ctx.SaveChangesAsync();
        }

        await using var ctx2 = CreateDbContext(db);
        var queue = await new TatamiQueueService(ctx2).GetQueueAsync(tid, tatamiId, CancellationToken.None);

        Assert.NotNull(queue?.Current);
        Assert.Equal(fightId, queue!.Current!.Id);
        Assert.False(queue.Current.IsGoldenScore, "Fight with unequal waza-ari (leading score) should not enter golden-score phase.");
    }

    /// <summary>
    /// A pending fight (not yet started) must never report <c>IsGoldenScore = true</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetQueue_PendingFight_IsGoldenScoreFalse()
    {
        var db = CreateDatabasePath();
        Guid tid, cid, tatamiId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, tatamiId) = await SeedAsync(ctx, 4);
        }

        var roundOne = await RoundOneFightsAsync(db, cid);

        await using (var ctx = CreateDbContext(db))
        {
            await CreateMatchService(ctx).AssignTatamiAsync(roundOne[0].Id, tatamiId, "Admin", CancellationToken.None);
        }

        await using var ctx2 = CreateDbContext(db);
        var queue = await new TatamiQueueService(ctx2).GetQueueAsync(tid, tatamiId, CancellationToken.None);

        Assert.NotNull(queue?.Current);
        Assert.False(queue!.Current!.IsGoldenScore, "Pending fight must not be in golden-score phase.");
    }
}
