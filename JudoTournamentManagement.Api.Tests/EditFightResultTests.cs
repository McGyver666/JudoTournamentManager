using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Hubs;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JudoTournamentManagement.Api.Tests;

/// <summary>
/// Unit tests for <see cref="MatchService.EditResultAsync"/> (Kampfübersicht-Korrektur).
/// </summary>
[Trait("Category", "UnitTest")]
public sealed class EditFightResultTests
{
    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoEditResultTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "editresult.db");
    }

    private static AppDbContext CreateDbContext(string path)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;
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
        return new MatchService(ctx,
            new AuditLogService(ctx, NullLogger<AuditLogService>.Instance),
            mockHub.Object,
            new Mock<IBracketService>().Object,
            new Mock<IRankingService>().Object,
            NullLogger<MatchService>.Instance);
    }

    /// <summary>Seeds a 4-athlete single-elimination bracket: 2 R1 fights → 1 final.</summary>
    private static async Task<(string Db, Guid TournamentId, Guid CategoryId, List<Guid> AthleteIds)>
        SeedBracketAsync(int athletes = 4)
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();

        var mockPresets = new Mock<ICategoryPresetsStore>();
        mockPresets.Setup(p => p.SeedDefaultsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance, mockPresets.Object);
        var t = await tStore.CreateAsync("T", new DateOnly(2026, 1, 1), "V", "O", CancellationToken.None);

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club = await clubStore.CreateAsync(t.Id, "JC", null, null, null, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var ids = new List<Guid>();
        for (int i = 0; i < athletes; i++)
        {
            var a = await athleteStore.CreateAsync(
                t.Id, club!.Id, $"A{i:D2}", "Test", 2000, Models.Gender.Male, null, null, 1, true, CancellationToken.None);
            ids.Add(a!.Id);
        }

        var catStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var cat = await catStore.CreateAsync(t.Id, "U18 M -73", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        var regStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        foreach (var id in ids)
        {
            var reg = await regStore.CreateAsync(t.Id, id, 25m, null, false, CancellationToken.None);
            await regStore.AssignCategoryAsync(reg!.Id, cat!.Id, CancellationToken.None);
        }

        var bracketService = new BracketService(ctx, NullLogger<BracketService>.Instance);
        await bracketService.GenerateAsync(t.Id, cat!.Id, BracketFormat.SingleElimination, CancellationToken.None);

        return (db, t.Id, cat.Id, ids);
    }

    private static async Task<List<FightRecord>> GetFightsAsync(string db, Guid categoryId)
    {
        await using var ctx = CreateDbContext(db);
        return await ctx.Fights.AsNoTracking().Where(f => f.CategoryId == categoryId)
            .OrderBy(f => f.Round).ThenBy(f => f.FightNumber).ToListAsync();
    }

    private static EditFightResultRequest BuildRequest(FightRecord fight, Guid winnerId,
        int whiteIppon = 1, int whiteWaza = 0, int whiteYuko = 0, int whiteShido = 0,
        int blueIppon = 0, int blueWaza = 0, int blueYuko = 0, int blueShido = 0,
        bool confirmed = false)
        => new()
        {
            WinnerId = winnerId,
            WhiteIpponCount = whiteIppon, WhiteWazaAriCount = whiteWaza,
            WhiteYukoCount = whiteYuko, WhitePenalties = whiteShido,
            BlueIpponCount = blueIppon, BlueWazaAriCount = blueWaza,
            BlueYukoCount = blueYuko, BluePenalties = blueShido,
            Confirmed = confirmed
        };

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EditResult_FightNotFound_ReturnsFightNotFound()
    {
        var (db, _, _, _) = await SeedBracketAsync();
        await using var ctx = CreateDbContext(db);
        var svc = CreateService(ctx);

        var result = await svc.EditResultAsync(Guid.NewGuid(),
            new EditFightResultRequest { WinnerId = Guid.NewGuid() }, "admin", CancellationToken.None);

        Assert.Equal(EditResultStatus.FightNotFound, result.Status);
    }

    [Fact]
    public async Task EditResult_NonCompletedFight_ReturnsInvalidState()
    {
        var (db, _, cid, athletes) = await SeedBracketAsync();

        // The final fight is Pending at this point (not yet completed).
        var fights = await GetFightsAsync(db, cid);
        var finalFight = fights.Single(f => f.Round == 2);

        await using var ctx = CreateDbContext(db);
        var svc = CreateService(ctx);

        var result = await svc.EditResultAsync(finalFight.Id,
            BuildRequest(finalFight, athletes[0]), "admin", CancellationToken.None);

        Assert.Equal(EditResultStatus.InvalidState, result.Status);
    }

    [Fact]
    public async Task EditResult_GroupStageFight_ReturnsInvalidState()
    {
        // Use proper seeded IDs to satisfy FK constraints, then inject a GroupStage fight.
        var (db, tid, cid, athletes) = await SeedBracketAsync();
        var whiteId = athletes[0]; var blueId = athletes[1];

        Guid groupFightId;
        await using (var ctx = CreateDbContext(db))
        {
            var groupFight = new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid,
                BracketType = FightBracketType.GroupStage.ToString(),
                Round = 1, FightNumber = 99,
                Status = FightStatus.Completed.ToString(), IsBye = false,
                WhiteAthleteId = whiteId, BlueAthleteId = blueId, WinnerId = whiteId,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
                StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
            ctx.Fights.Add(groupFight);
            await ctx.SaveChangesAsync();
            groupFightId = groupFight.Id;
        }

        await using (var ctx = CreateDbContext(db))
        {
            var fight = await ctx.Fights.FirstAsync(f => f.Id == groupFightId);
            var result = await CreateService(ctx).EditResultAsync(fight.Id,
                BuildRequest(fight, whiteId), "admin", CancellationToken.None);
            Assert.Equal(EditResultStatus.InvalidState, result.Status);
        }
    }

    [Fact]
    public async Task EditResult_WinnerNotParticipant_ReturnsWinnerNotParticipant()
    {
        var (db, _, cid, athletes) = await SeedBracketAsync();

        // Complete R1F1 so it can be edited.
        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            var fights = await ctx.Fights.Where(f => f.CategoryId == cid).ToListAsync();
            var r1f1 = fights.Single(f => f.Round == 1 && f.FightNumber == 1);
            r1f1.Status = FightStatus.Completed.ToString();
            r1f1.WinnerId = r1f1.WhiteAthleteId;
            r1f1.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3);
            r1f1.CompletedAtUtc = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        var fights2 = await GetFightsAsync(db, cid);
        var r1f1Read = fights2.Single(f => f.Round == 1 && f.FightNumber == 1);

        await using var ctx2 = CreateDbContext(db);
        var result = await CreateService(ctx2).EditResultAsync(r1f1Read.Id,
            BuildRequest(r1f1Read, Guid.NewGuid()), "admin", CancellationToken.None);

        Assert.Equal(EditResultStatus.WinnerNotParticipant, result.Status);
    }

    [Fact]
    public async Task EditResult_PureScoreChange_NoWinnerChange_SavesAndReturnsSuccess()
    {
        var (db, _, cid, _) = await SeedBracketAsync();

        // Complete R1F1 with white winner.
        Guid r1f1Id;
        Guid? whiteId;
        await using (var ctx = CreateDbContext(db))
        {
            var f = await ctx.Fights.FirstAsync(f => f.CategoryId == cid && f.Round == 1 && f.FightNumber == 1);
            r1f1Id = f.Id;
            whiteId = f.WhiteAthleteId;
            f.Status = FightStatus.Completed.ToString();
            f.WinnerId = whiteId;
            f.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3);
            f.CompletedAtUtc = DateTimeOffset.UtcNow;
            f.WhiteIpponCount = 1; f.WhiteScore = 10;
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            var request = new EditFightResultRequest
            {
                WinnerId = whiteId!.Value,
                WhiteIpponCount = 0, WhiteWazaAriCount = 2, WhiteYukoCount = 0, WhitePenalties = 1,
                BlueIpponCount = 0, BlueWazaAriCount = 0, BlueYukoCount = 1, BluePenalties = 0,
                Confirmed = false
            };
            var result = await svc.EditResultAsync(r1f1Id, request, "admin", CancellationToken.None);
            Assert.Equal(EditResultStatus.Success, result.Status);
        }

        var updated = await GetFightsAsync(db, cid);
        var f1 = updated.Single(f => f.Id == r1f1Id);
        Assert.Equal(0, f1.WhiteIpponCount);
        Assert.Equal(2, f1.WhiteWazaAriCount);
        Assert.Equal(0, f1.WhiteYukoCount);
        // Score = ScoreValue(0 ippon, 2 waza-ari, 0 yuko) = 0*10 + 2*7 + 0 = 14
        Assert.Equal(14, f1.WhiteScore);
        Assert.Equal(1, f1.WhitePenalties);
    }

    [Fact]
    public async Task EditResult_WinnerChange_NoStartedDownstream_SavesDirectly()
    {
        var (db, _, cid, _) = await SeedBracketAsync();

        Guid r1f1Id;
        Guid? whiteId, blueId;
        await using (var ctx = CreateDbContext(db))
        {
            var f = await ctx.Fights.FirstAsync(f => f.CategoryId == cid && f.Round == 1 && f.FightNumber == 1);
            r1f1Id = f.Id; whiteId = f.WhiteAthleteId; blueId = f.BlueAthleteId;
            f.Status = FightStatus.Completed.ToString();
            f.WinnerId = whiteId;
            f.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3);
            f.CompletedAtUtc = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            var request = BuildRequest(
                await ctx.Fights.FirstAsync(f => f.Id == r1f1Id),
                blueId!.Value);
            var result = await svc.EditResultAsync(r1f1Id, request, "admin", CancellationToken.None);
            Assert.Equal(EditResultStatus.Success, result.Status);
        }

        var updated = await GetFightsAsync(db, cid);
        Assert.Equal(blueId, updated.Single(f => f.Id == r1f1Id).WinnerId);
        // Final fight (R2F1) should now have blueId as white athlete via progression
        var final = updated.Single(f => f.Round == 2);
        Assert.Equal(blueId, final.WhiteAthleteId);
    }

    [Fact]
    public async Task EditResult_WinnerChange_StartedDownstreamFight_WithoutConfirm_ReturnsConfirmationRequired()
    {
        var (db, _, cid, _) = await SeedBracketAsync();

        Guid r1f1Id;
        Guid? whiteId, blueId;

        // Complete R1F1 (white wins) and start the final fight.
        await using (var ctx = CreateDbContext(db))
        {
            var fights = await ctx.Fights.Where(f => f.CategoryId == cid).ToListAsync();
            var r1f1 = fights.Single(f => f.Round == 1 && f.FightNumber == 1);
            var r1f2 = fights.Single(f => f.Round == 1 && f.FightNumber == 2);
            var final = fights.Single(f => f.Round == 2);

            r1f1Id = r1f1.Id; whiteId = r1f1.WhiteAthleteId; blueId = r1f1.BlueAthleteId;

            r1f1.Status = FightStatus.Completed.ToString();
            r1f1.WinnerId = whiteId;
            r1f1.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
            r1f1.CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);

            r1f2.Status = FightStatus.Completed.ToString();
            r1f2.WinnerId = r1f2.WhiteAthleteId;
            r1f2.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-8);
            r1f2.CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-4);

            // Wire up final and start it.
            final.WhiteAthleteId = whiteId;
            final.BlueAthleteId = r1f2.WhiteAthleteId;
            final.Status = FightStatus.InProgress.ToString();
            final.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            var request = BuildRequest(
                await ctx.Fights.FirstAsync(f => f.Id == r1f1Id),
                blueId!.Value,
                confirmed: false);
            var result = await svc.EditResultAsync(r1f1Id, request, "admin", CancellationToken.None);

            Assert.Equal(EditResultStatus.ConfirmationRequired, result.Status);
            Assert.NotNull(result.AffectedFights);
            Assert.NotEmpty(result.AffectedFights);
        }

        // Database must be unchanged.
        var unchanged = await GetFightsAsync(db, cid);
        Assert.Equal(whiteId, unchanged.Single(f => f.Id == r1f1Id).WinnerId);
    }

    [Fact]
    public async Task EditResult_WinnerChange_StartedDownstreamFight_WithConfirm_ResetsDownstreamAndSaves()
    {
        var (db, _, cid, _) = await SeedBracketAsync();

        Guid r1f1Id, finalId;
        Guid? whiteId, blueId;

        await using (var ctx = CreateDbContext(db))
        {
            var fights = await ctx.Fights.Where(f => f.CategoryId == cid).ToListAsync();
            var r1f1 = fights.Single(f => f.Round == 1 && f.FightNumber == 1);
            var r1f2 = fights.Single(f => f.Round == 1 && f.FightNumber == 2);
            var final = fights.Single(f => f.Round == 2);

            r1f1Id = r1f1.Id; finalId = final.Id;
            whiteId = r1f1.WhiteAthleteId; blueId = r1f1.BlueAthleteId;

            r1f1.Status = FightStatus.Completed.ToString();
            r1f1.WinnerId = whiteId;
            r1f1.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
            r1f1.CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
            r1f1.WhiteIpponCount = 1; r1f1.WhiteScore = 10;

            r1f2.Status = FightStatus.Completed.ToString();
            r1f2.WinnerId = r1f2.WhiteAthleteId;
            r1f2.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-9);
            r1f2.CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-4);

            final.WhiteAthleteId = whiteId;
            final.BlueAthleteId = r1f2.WhiteAthleteId;
            final.Status = FightStatus.Completed.ToString();
            final.WinnerId = whiteId;
            final.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3);
            final.CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            final.WhiteIpponCount = 1; final.WhiteScore = 10;
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateDbContext(db))
        {
            var svc = CreateService(ctx);
            var request = BuildRequest(
                await ctx.Fights.FirstAsync(f => f.Id == r1f1Id),
                blueId!.Value,
                confirmed: true);
            var result = await svc.EditResultAsync(r1f1Id, request, "admin", CancellationToken.None);
            Assert.Equal(EditResultStatus.Success, result.Status);
        }

        var updated = await GetFightsAsync(db, cid);

        // R1F1 now has blue as winner.
        Assert.Equal(blueId, updated.Single(f => f.Id == r1f1Id).WinnerId);

        // Final must be reset to Pending with cleared scores.
        var finalUpdated = updated.Single(f => f.Id == finalId);
        Assert.Equal(FightStatus.Pending.ToString(), finalUpdated.Status);
        Assert.Null(finalUpdated.WinnerId);
        Assert.Null(finalUpdated.StartedAtUtc);
        Assert.Equal(0, finalUpdated.WhiteIpponCount);
        Assert.Equal(0, finalUpdated.WhiteScore);
    }
}
