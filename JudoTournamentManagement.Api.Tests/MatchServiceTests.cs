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
            var pauseResult = await svc.PauseAsync(final.Id, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, pauseResult);
        }

        var paused = (await ReadFightsAsync(db, cid)).Single();
        Assert.Equal(FightStatus.Paused.ToString(), paused.Status);
        Assert.NotNull(paused.PausedAtUtc);

        await using (var ctx = CreateDbContext(db))
        {
            var resumeResult = await CreateService(ctx).ResumeAsync(final.Id, "Tisch1", CancellationToken.None);
            Assert.Equal(MatchActionResult.Success, resumeResult);
        }

        var resumed = (await ReadFightsAsync(db, cid)).Single();
        Assert.Equal(FightStatus.InProgress.ToString(), resumed.Status);
        Assert.Null(resumed.PausedAtUtc);
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
}
