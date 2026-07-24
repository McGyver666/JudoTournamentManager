using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Hubs;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

/// <summary>
/// Unit tests for <see cref="MatchService"/> covering start, scoring, winner confirmation,
/// bracket progression, result correction and audit logging (F-02, F-03, I-01).
/// </summary>
public sealed class MatchServiceTests
{
    // ─── Infrastructure helpers ───────────────────────────────────────────────

    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "match.db");
    }

    private static AppDbContext CreateDbContext(string path)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}").Options;
        return new AppDbContext(opts);
    }

    private static MatchService CreateService(AppDbContext ctx)
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

    private static async Task<(Guid TournamentId, Guid CategoryId, List<Guid> AthleteIds)>
        SeedBracketAsync(AppDbContext ctx, int athleteCount, BracketFormat format = BracketFormat.SingleElimination)
    {
        var mockPresets = new Mock<ICategoryPresetsStore>();
        mockPresets.Setup(p => p.SeedDefaultsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance, mockPresets.Object);
        var t = await tStore.CreateAsync("T", new DateOnly(2026, 1, 1), "V", "O", CancellationToken.None);

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club = await clubStore.CreateAsync(t.Id, "JC Test", null, null, null, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var athleteIds = new List<Guid>();
        for (int i = 0; i < athleteCount; i++)
        {
            var a = await athleteStore.CreateAsync(
                t.Id, club!.Id, $"A{i:D2}", "Tester", 2000 + i, Gender.Male, null, null, 1, true, CancellationToken.None);
            athleteIds.Add(a!.Id);
        }

        var catStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var cat = await catStore.CreateAsync(t.Id, "U18 M -73", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        var regStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        foreach (var aid in athleteIds)
        {
            var reg = await regStore.CreateAsync(t.Id, aid, 25.0m, null, false, CancellationToken.None);
            await regStore.AssignCategoryAsync(reg!.Id, cat!.Id, CancellationToken.None);
        }

        var bracketService = new BracketService(ctx, NullLogger<BracketService>.Instance);
        await bracketService.GenerateAsync(t.Id, cat!.Id, format, CancellationToken.None);

        return (t.Id, cat.Id, athleteIds);
    }

    private static async Task<List<FightRecord>> ReadFightsAsync(string db, Guid categoryId)
    {
        await using var ctx = CreateDbContext(db);
        return await ctx.Fights
            .AsNoTracking()
            .Where(f => f.CategoryId == categoryId)
            .OrderBy(f => f.BracketType).ThenBy(f => f.Round).ThenBy(f => f.FightNumber)
            .ToListAsync();
    }

    private static FightRecord MainFight(IEnumerable<FightRecord> fights, int round, int fightNumber) =>
        fights.Single(f => f.BracketType == FightBracketType.Main.ToString()
                           && f.Round == round && f.FightNumber == fightNumber);

    // ─── Start ────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Start_PendingFight_SetsInProgress()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using (var ctx = CreateDbContext(db))
        {
            var result = await CreateService(ctx).StartAsync(final.Id, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        var updated = (await ReadFightsAsync(db, cid)).Single();
        Assert.Equal(FightStatus.InProgress.ToString(), updated.Status);
        Assert.NotNull(updated.StartedAtUtc);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Start_PendingFight_LocksCategory()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        await using (var verifyCtx = CreateDbContext(db))
        {
            var categoryBefore = await verifyCtx.Categories.FirstAsync(c => c.Id == cid);
            Assert.False(categoryBefore.IsLocked);
        }

        var fight = (await ReadFightsAsync(db, cid)).Single();

        await using (var ctx = CreateDbContext(db))
        {
            var result = await CreateService(ctx).StartAsync(fight.Id, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        await using (var verifyCtx = CreateDbContext(db))
        {
            var categoryAfter = await verifyCtx.Categories.FirstAsync(c => c.Id == cid);
            Assert.True(categoryAfter.IsLocked);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Start_ByeFight_ReturnsInvalidState()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 3); // bracket of 4 -> one bye
        }

        var bye = (await ReadFightsAsync(db, cid)).First(f => f.IsBye);

        await using var ctx2 = CreateDbContext(db);
        var result = await CreateService(ctx2).StartAsync(bye.Id, "Tisch1", CancellationToken.None);
        Assert.Equal(MatchActionResult.InvalidState, result);

        var category = await ctx2.Categories.FirstAsync(c => c.Id == cid);
        Assert.False(category.IsLocked);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Start_UnknownFight_ReturnsFightNotFound()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();

        var result = await CreateService(ctx).StartAsync(Guid.NewGuid(), "Tisch1", CancellationToken.None);
        Assert.Equal(MatchActionResult.FightNotFound, result);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task AssignTatami_PersistsAcrossDbContexts()
    {
        var db = CreateDatabasePath();
        Guid tid, cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        Guid tatamiId;
        await using (var ctx = CreateDbContext(db))
        {
            var tatami = await new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance)
                .CreateAsync(tid, "Tatami 1", 0, CancellationToken.None);
            tatamiId = tatami.Id;
        }

        var fightId = (await ReadFightsAsync(db, cid)).Single().Id;

        await using (var ctx = CreateDbContext(db))
        {
            var result = await CreateService(ctx)
                .AssignTatamiAsync(fightId, tatamiId, "Admin", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        await using (var ctx = CreateDbContext(db))
        {
            var store = new SqliteFightsStore(ctx, NullLogger<SqliteFightsStore>.Instance);
            var fight = await store.GetByIdAsync(fightId, CancellationToken.None);

            Assert.NotNull(fight);
            Assert.Equal(tatamiId, fight!.TatamiId);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task AssignTatami_GetAllAsyncReturnsTatamiIdAfterReload()
    {
        var db = CreateDatabasePath();
        Guid tid, cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        Guid tatamiId;
        await using (var ctx = CreateDbContext(db))
        {
            var tatami = await new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance)
                .CreateAsync(tid, "Tatami 1", 0, CancellationToken.None);
            tatamiId = tatami.Id;
        }

        var fightId = (await ReadFightsAsync(db, cid)).Single().Id;

        await using (var ctx = CreateDbContext(db))
        {
            var result = await CreateService(ctx)
                .AssignTatamiAsync(fightId, tatamiId, "Admin", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        await using (var ctx = CreateDbContext(db))
        {
            var store = new SqliteFightsStore(ctx, NullLogger<SqliteFightsStore>.Instance);
            var fights = await store.GetAllAsync(tid, cid, CancellationToken.None);

            var fight = Assert.Single(fights);
            Assert.Equal(fightId, fight.Id);
            Assert.Equal(tatamiId, fight.TatamiId);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Pause_ThenResume_TransitionsFightState()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(final.Id, "Tisch1", CancellationToken.None);
            await svc.StartOsaeKomiAsync(final.Id, "white", "Tisch1", CancellationToken.None);
            var pauseResult = await svc.PauseAsync(final.Id, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, pauseResult);
        }

        var paused = (await ReadFightsAsync(db, cid)).Single();
        Assert.Equal(FightStatus.Paused.ToString(), paused.Status);
        Assert.NotNull(paused.PausedAtUtc);
        Assert.Null(paused.OsaeKomiSide);
        Assert.Null(paused.OsaeKomiStartedAtUtc);

        await using (var ctx = CreateDbContext(db))
        {
            var resumeResult = await CreateService(ctx).ResumeAsync(final.Id, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, resumeResult);
        }

        var resumed = (await ReadFightsAsync(db, cid)).Single();
        Assert.Equal(FightStatus.InProgress.ToString(), resumed.Status);
        Assert.Null(resumed.PausedAtUtc);
        Assert.Null(resumed.OsaeKomiSide);
        Assert.Null(resumed.OsaeKomiStartedAtUtc);
    }

    // ─── Scoring ──────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task RecordScore_InProgressFight_PersistsValues()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(final.Id, "Tisch1", CancellationToken.None);
            var result = await svc.RecordScoreAsync(final.Id, 10, 1, 0, 2, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        var updated = (await ReadFightsAsync(db, cid)).Single();
        Assert.Equal(10, updated.WhiteScore);
        Assert.Equal(1, updated.BlueScore);
        Assert.Equal(0, updated.WhitePenalties);
        Assert.Equal(2, updated.BluePenalties);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task RecordScore_PendingFight_ReturnsInvalidState()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using var ctx2 = CreateDbContext(db);
        var result = await CreateService(ctx2).RecordScoreAsync(final.Id, 10, 0, 0, 0, "Tisch1", CancellationToken.None);
        Assert.Equal(MatchActionResult.InvalidState, result);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task AdjustScore_AddYuko_UpdatesBucketAndTotal()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(final.Id, "Tisch1", CancellationToken.None);
            var result = await svc.AdjustScoreAsync(final.Id, "white", ScoreType.Yuko, 1, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        var updated = (await ReadFightsAsync(db, cid)).Single();
        Assert.Equal(1, updated.WhiteYukoCount);
        Assert.Equal(1, updated.WhiteScore);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task AdjustScore_RemoveMissingBucket_ReturnsInvalidState()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using var ctx2 = CreateDbContext(db);
        var svc = CreateService(ctx2);
        await svc.StartAsync(final.Id, "Tisch1", CancellationToken.None);
        var result = await svc.AdjustScoreAsync(final.Id, "blue", ScoreType.WazaAri, -1, "Tisch1", CancellationToken.None);
        Assert.Equal(MatchActionResult.InvalidState, result);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task StartOsaeKomi_InProgressFight_SetsActiveHold()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(final.Id, "Tisch1", CancellationToken.None);
            var result = await svc.StartOsaeKomiAsync(final.Id, "blue", "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        var updated = (await ReadFightsAsync(db, cid)).Single();
        Assert.Equal("Blue", updated.OsaeKomiSide);
        Assert.NotNull(updated.OsaeKomiStartedAtUtc);
    }

    // ─── Confirm ──────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Confirm_SetsWinner_Completes_AndWritesAudit()
    {
        var db = CreateDatabasePath();
        Guid tid, cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();
        var winner = final.WhiteAthleteId!.Value;

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(final.Id, "Tisch1", CancellationToken.None);
            var result = await svc.ConfirmResultAsync(final.Id, winner, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        var updated = (await ReadFightsAsync(db, cid)).Single();
        Assert.Equal(FightStatus.Completed.ToString(), updated.Status);
        Assert.Equal(winner, updated.WinnerId);
        Assert.NotNull(updated.CompletedAtUtc);

        await using var read = CreateDbContext(db);
        var entries = await read.AuditLogs.AsNoTracking().Where(a => a.TournamentId == tid).ToListAsync();
        Assert.Contains(entries, e => e.Action == "ResultConfirmed" && e.EntityId == final.Id);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Confirm_StoresLastFightMetadata_ForBothAthletes()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();
        var whiteAthleteId = final.WhiteAthleteId!.Value;
        var blueAthleteId = final.BlueAthleteId!.Value;

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(final.Id, "Tisch1", CancellationToken.None);

            var trackedFight = await ctx.Fights.SingleAsync(f => f.Id == final.Id);
            trackedFight.StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-95);
            await ctx.SaveChangesAsync();

            var result = await svc.ConfirmResultAsync(final.Id, whiteAthleteId, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        await using var verify = CreateDbContext(db);
        var whiteAthlete = await verify.Athletes.AsNoTracking().SingleAsync(a => a.Id == whiteAthleteId);
        var blueAthlete = await verify.Athletes.AsNoTracking().SingleAsync(a => a.Id == blueAthleteId);

        Assert.NotNull(whiteAthlete.LastFightEndedAtUtc);
        Assert.NotNull(blueAthlete.LastFightEndedAtUtc);
        Assert.NotNull(whiteAthlete.LastFightDurationSeconds);
        Assert.NotNull(blueAthlete.LastFightDurationSeconds);
        Assert.InRange(whiteAthlete.LastFightDurationSeconds!.Value, 90, 120);
        Assert.InRange(blueAthlete.LastFightDurationSeconds!.Value, 90, 120);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Confirm_WithNonParticipant_ReturnsWinnerNotParticipant()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using var ctx2 = CreateDbContext(db);
        var svc = CreateService(ctx2);
        await svc.StartAsync(final.Id, "Tisch1", CancellationToken.None);
        var result = await svc.ConfirmResultAsync(final.Id, Guid.NewGuid(), "Tisch1", CancellationToken.None);
        Assert.Equal(MatchActionResult.WinnerNotParticipant, result);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Confirm_PropagatesWinnerToNextRound()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 4);
        }

        var fights = await ReadFightsAsync(db, cid);
        var sf1 = MainFight(fights, 1, 1);
        var sf2 = MainFight(fights, 1, 2);
        var winner1 = sf1.WhiteAthleteId!.Value;
        var winner2 = sf2.BlueAthleteId!.Value;

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(sf1.Id, "T1", CancellationToken.None);
            await svc.ConfirmResultAsync(sf1.Id, winner1, "T1", CancellationToken.None);
            await svc.StartAsync(sf2.Id, "T1", CancellationToken.None);
            await svc.ConfirmResultAsync(sf2.Id, winner2, "T1", CancellationToken.None);
        }

        var final = MainFight(await ReadFightsAsync(db, cid), 2, 1);
        Assert.Equal(winner1, final.WhiteAthleteId);
        Assert.Equal(winner2, final.BlueAthleteId);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Confirm_RoundRobin_DoesNotRewireFutureRounds()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 5, BracketFormat.RoundRobin);
        }

        var before = await ReadFightsAsync(db, cid);
        var fightToConfirm = before.First(f => f.Round == 1 && !f.IsBye);
        var winner = fightToConfirm.WhiteAthleteId!.Value;

        var expectedFutureRounds = before
            .Where(f => f.Round > 1)
            .ToDictionary(
                f => f.Id,
                f => new { f.WhiteAthleteId, f.BlueAthleteId, f.IsBye });

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(fightToConfirm.Id, "T1", CancellationToken.None);
            var result = await svc.ConfirmResultAsync(fightToConfirm.Id, winner, "T1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        var after = await ReadFightsAsync(db, cid);
        foreach (var fight in after.Where(f => f.Round > 1))
        {
            var expected = expectedFutureRounds[fight.Id];
            Assert.Equal(expected.WhiteAthleteId, fight.WhiteAthleteId);
            Assert.Equal(expected.BlueAthleteId, fight.BlueAthleteId);
            Assert.Equal(expected.IsBye, fight.IsBye);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Confirm_Semifinals_PopulatesRepechageWithLosers()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 4, BracketFormat.SingleEliminationWithRepechage);
        }

        var fights = await ReadFightsAsync(db, cid);
        var sf1 = MainFight(fights, 1, 1);
        var sf2 = MainFight(fights, 1, 2);
        var winner1 = sf1.WhiteAthleteId!.Value;
        var loser1 = sf1.BlueAthleteId!.Value;
        var winner2 = sf2.WhiteAthleteId!.Value;
        var loser2 = sf2.BlueAthleteId!.Value;

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(sf1.Id, "T1", CancellationToken.None);
            await svc.ConfirmResultAsync(sf1.Id, winner1, "T1", CancellationToken.None);
            await svc.StartAsync(sf2.Id, "T1", CancellationToken.None);
            await svc.ConfirmResultAsync(sf2.Id, winner2, "T1", CancellationToken.None);
        }

        var repechage = (await ReadFightsAsync(db, cid))
            .Single(f => f.BracketType == FightBracketType.Repechage.ToString());
        Assert.Equal(loser1, repechage.WhiteAthleteId);
        Assert.Equal(loser2, repechage.BlueAthleteId);
    }

    // ─── Correction ───────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Correct_ChangesWinner_RecalculatesProgression_AndAudits()
    {
        var db = CreateDatabasePath();
        Guid tid, cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, _) = await SeedBracketAsync(ctx, 4);
        }

        var fights = await ReadFightsAsync(db, cid);
        var sf1 = MainFight(fights, 1, 1);
        var originalWinner = sf1.WhiteAthleteId!.Value;
        var correctedWinner = sf1.BlueAthleteId!.Value;

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            await svc.StartAsync(sf1.Id, "T1", CancellationToken.None);
            await svc.ConfirmResultAsync(sf1.Id, originalWinner, "T1", CancellationToken.None);
        }

        // Final white slot should now hold the original winner.
        Assert.Equal(originalWinner, MainFight(await ReadFightsAsync(db, cid), 2, 1).WhiteAthleteId);

        await using (var ctx = CreateDbContext(db))
        {
            var result = await CreateService(ctx).CorrectResultAsync(sf1.Id, correctedWinner, "Admin", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        var afterFights = await ReadFightsAsync(db, cid);
        Assert.Equal(correctedWinner, MainFight(afterFights, 1, 1).WinnerId);
        Assert.Equal(correctedWinner, MainFight(afterFights, 2, 1).WhiteAthleteId);

        await using var read = CreateDbContext(db);
        var correction = await read.AuditLogs.AsNoTracking()
            .FirstAsync(a => a.Action == "ResultCorrected" && a.EntityId == sf1.Id);
        Assert.Contains(originalWinner.ToString(), correction.Details);
        Assert.Contains(correctedWinner.ToString(), correction.Details);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Correct_PendingFight_ReturnsInvalidState()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using var ctx2 = CreateDbContext(db);
        var result = await CreateService(ctx2)
            .CorrectResultAsync(final.Id, final.WhiteAthleteId!.Value, "Admin", CancellationToken.None);
        Assert.Equal(MatchActionResult.InvalidState, result);
    }

    // ─── Tatami assignment ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task AssignTatami_ValidTatami_SetsAssignment()
    {
        var db = CreateDatabasePath();
        Guid tid, cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (tid, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        Guid tatamiId;
        await using (var ctx = CreateDbContext(db))
        {
            var tatami = await new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance)
                .CreateAsync(tid, "Tatami 1", 0, CancellationToken.None);
            tatamiId = tatami.Id;
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using (var ctx = CreateDbContext(db))
        {
            var result = await CreateService(ctx).AssignTatamiAsync(final.Id, tatamiId, "Admin", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        Assert.Equal(tatamiId, (await ReadFightsAsync(db, cid)).Single().TatamiId);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task AssignTatami_UnknownTatami_ReturnsInvalidState()
    {
        var db = CreateDatabasePath();
        Guid cid;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, cid, _) = await SeedBracketAsync(ctx, 2);
        }

        var final = (await ReadFightsAsync(db, cid)).Single();

        await using var ctx2 = CreateDbContext(db);
        var result = await CreateService(ctx2)
            .AssignTatamiAsync(final.Id, Guid.NewGuid(), "Admin", CancellationToken.None);
        Assert.Equal(MatchActionResult.InvalidState, result);
    }

    // ─── StopOsaeKomi – Auto-Scoring ──────────────────────────────────────────

    /// <summary>Seeds a tournament+fight directly into the DB and starts the fight.</summary>
    private static async Task<(Guid TournamentId, Guid FightId)> SeedOsaeKomiFightAsync(
        AppDbContext ctx,
        int osaeKomiIpponSeconds = 20,
        int osaeKomiWazaAriSeconds = 10,
        int osaeKomiYukoSeconds = 5,
        bool osaeKomiYukoEnabled = true)
    {
        var tid = Guid.NewGuid();
        ctx.Tournaments.Add(new JudoTournamentManagement.Api.Data.TournamentRecord
        {
            Id = tid,
            Name = "Test",
            Date = new DateOnly(2026, 1, 1),
            Venue = "V",
            Organizer = "O",
            AccentSideColor = "Blue",
            OsaeKomiIpponSeconds = osaeKomiIpponSeconds,
            OsaeKomiWazaAriSeconds = osaeKomiWazaAriSeconds,
            OsaeKomiYukoSeconds = osaeKomiYukoSeconds,
            OsaeKomiYukoEnabled = osaeKomiYukoEnabled,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        var cid = Guid.NewGuid();
        ctx.Categories.Add(new JudoTournamentManagement.Api.Data.CategoryRecord
        {
            Id = cid,
            TournamentId = tid,
            Name = "U18 M -73",
            AgeGroup = "U18",
            Gender = "Male",
            MatchDurationSeconds = 300,
            GoldenScoreEnabled = false,
            GoldenScoreDurationSeconds = 180,
            DrawFormat = null,
            IsLocked = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        var whiteId = Guid.NewGuid();
        var blueId = Guid.NewGuid();
        var fid = Guid.NewGuid();
        ctx.Fights.Add(new JudoTournamentManagement.Api.Data.FightRecord
        {
            Id = fid,
            TournamentId = tid,
            CategoryId = cid,
            BracketType = "Main",
            Round = 1,
            FightNumber = 1,
            WhiteAthleteId = whiteId,
            BlueAthleteId = blueId,
            Status = "InProgress",
            StartedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        await ctx.SaveChangesAsync();
        return (tid, fid);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task StopOsaeKomi_Hold20s_AwardsIppon()
    {
        var db = CreateDatabasePath();
        Guid fightId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, fightId) = await SeedOsaeKomiFightAsync(ctx);
            await CreateService(ctx).StartOsaeKomiAsync(fightId, "white", "t", CancellationToken.None);
            var f = ctx.Fights.Single(x => x.Id == fightId);
            f.OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-20);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
        {
            var result = await CreateService(ctx).StopOsaeKomiAsync(fightId, "t", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, result);
        }

        await using (var ctx = CreateDbContext(db))
            await CreateService(ctx).StopOsaeKomiAsync(fightId, "t", CancellationToken.None);

        await using var ctxRead1 = CreateDbContext(db);
        var updated = await ctxRead1.Fights.AsNoTracking().SingleAsync(x => x.Id == fightId);
        Assert.Equal(1, updated.WhiteIpponCount);
        Assert.Equal(0, updated.WhiteWazaAriCount);
        Assert.Equal(FightStatus.Paused.ToString(), updated.Status);
        Assert.NotNull(updated.PausedAtUtc);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task StopOsaeKomi_Hold12s_AwardsWazaAri()
    {
        var db = CreateDatabasePath();
        Guid fightId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, fightId) = await SeedOsaeKomiFightAsync(ctx);
            await CreateService(ctx).StartOsaeKomiAsync(fightId, "white", "t", CancellationToken.None);
            var f = ctx.Fights.Single(x => x.Id == fightId);
            f.OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-12);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
            await CreateService(ctx).StopOsaeKomiAsync(fightId, "t", CancellationToken.None);

        await using var ctx2 = CreateDbContext(db);
        var updated = await ctx2.Fights.AsNoTracking().SingleAsync(x => x.Id == fightId);
        Assert.Equal(0, updated.WhiteIpponCount);
        Assert.Equal(1, updated.WhiteWazaAriCount);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task StopOsaeKomi_Hold7s_YukoEnabled_AwardsYuko()
    {
        var db = CreateDatabasePath();
        Guid fightId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, fightId) = await SeedOsaeKomiFightAsync(ctx, osaeKomiYukoEnabled: true);
            await CreateService(ctx).StartOsaeKomiAsync(fightId, "white", "t", CancellationToken.None);
            var f = ctx.Fights.Single(x => x.Id == fightId);
            f.OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-7);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
            await CreateService(ctx).StopOsaeKomiAsync(fightId, "t", CancellationToken.None);

        await using var ctx2 = CreateDbContext(db);
        var updated = await ctx2.Fights.AsNoTracking().SingleAsync(x => x.Id == fightId);
        Assert.Equal(0, updated.WhiteIpponCount);
        Assert.Equal(0, updated.WhiteWazaAriCount);
        Assert.Equal(1, updated.WhiteYukoCount);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task StopOsaeKomi_Hold7s_YukoDisabled_NoScore()
    {
        var db = CreateDatabasePath();
        Guid fightId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, fightId) = await SeedOsaeKomiFightAsync(ctx, osaeKomiYukoEnabled: false);
            await CreateService(ctx).StartOsaeKomiAsync(fightId, "white", "t", CancellationToken.None);
            var f = ctx.Fights.Single(x => x.Id == fightId);
            f.OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-7);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
            await CreateService(ctx).StopOsaeKomiAsync(fightId, "t", CancellationToken.None);

        await using var ctx2 = CreateDbContext(db);
        var updated = await ctx2.Fights.AsNoTracking().SingleAsync(x => x.Id == fightId);
        Assert.Equal(0, updated.WhiteIpponCount);
        Assert.Equal(0, updated.WhiteWazaAriCount);
        Assert.Equal(0, updated.WhiteYukoCount);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task StopOsaeKomi_Hold3s_NoScore()
    {
        var db = CreateDatabasePath();
        Guid fightId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, fightId) = await SeedOsaeKomiFightAsync(ctx);
            await CreateService(ctx).StartOsaeKomiAsync(fightId, "white", "t", CancellationToken.None);
            var f = ctx.Fights.Single(x => x.Id == fightId);
            f.OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-3);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
            await CreateService(ctx).StopOsaeKomiAsync(fightId, "t", CancellationToken.None);

        await using var ctx2 = CreateDbContext(db);
        var updated = await ctx2.Fights.AsNoTracking().SingleAsync(x => x.Id == fightId);
        Assert.Equal(0, updated.WhiteIpponCount);
        Assert.Equal(0, updated.WhiteWazaAriCount);
        Assert.Equal(0, updated.WhiteYukoCount);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task StopOsaeKomi_HolderHasWazaAri_Hold10s_AwardsIppon()
    {
        var db = CreateDatabasePath();
        Guid fightId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, fightId) = await SeedOsaeKomiFightAsync(ctx);
            // Pre-set Waza-ari for white.
            var f = ctx.Fights.Single(x => x.Id == fightId);
            f.WhiteWazaAriCount = 1;
            await ctx.SaveChangesAsync();
            await CreateService(ctx).StartOsaeKomiAsync(fightId, "white", "t", CancellationToken.None);
            f = ctx.Fights.Single(x => x.Id == fightId);
            f.OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-10);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
            await CreateService(ctx).StopOsaeKomiAsync(fightId, "t", CancellationToken.None);

        await using var ctx2 = CreateDbContext(db);
        var updated = await ctx2.Fights.AsNoTracking().SingleAsync(x => x.Id == fightId);
        // Second Waza-ari at 10 s → converted to Ippon.
        Assert.Equal(1, updated.WhiteIpponCount);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task StopOsaeKomi_CustomIpponThreshold_AwardsIpponAtCustomTime()
    {
        var db = CreateDatabasePath();
        Guid fightId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            // Use custom 15 s Ippon threshold.
            (_, fightId) = await SeedOsaeKomiFightAsync(ctx, osaeKomiIpponSeconds: 15);
            await CreateService(ctx).StartOsaeKomiAsync(fightId, "white", "t", CancellationToken.None);
            var f = ctx.Fights.Single(x => x.Id == fightId);
            f.OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-15);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
            await CreateService(ctx).StopOsaeKomiAsync(fightId, "t", CancellationToken.None);

        await using var ctx2 = CreateDbContext(db);
        var updated = await ctx2.Fights.AsNoTracking().SingleAsync(x => x.Id == fightId);
        Assert.Equal(1, updated.WhiteIpponCount);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task StopOsaeKomi_ClearsOsaeKomiFields()
    {
        var db = CreateDatabasePath();
        Guid fightId;
        await using (var ctx = CreateDbContext(db))
        {
            await ctx.Database.EnsureCreatedAsync();
            (_, fightId) = await SeedOsaeKomiFightAsync(ctx);
            await CreateService(ctx).StartOsaeKomiAsync(fightId, "blue", "t", CancellationToken.None);
        }

        await using (var ctx = CreateDbContext(db))
            await CreateService(ctx).StopOsaeKomiAsync(fightId, "t", CancellationToken.None);

        await using var ctx2 = CreateDbContext(db);
        var updated = await ctx2.Fights.AsNoTracking().SingleAsync(x => x.Id == fightId);
        Assert.Null(updated.OsaeKomiSide);
        Assert.Null(updated.OsaeKomiStartedAtUtc);
    }
}
