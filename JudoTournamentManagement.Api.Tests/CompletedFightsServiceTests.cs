using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class CompletedFightsServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoCompletedFightsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "completed.db");
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}").Options;
        return new AppDbContext(opts);
    }

    private static async Task<(Guid TournamentId, Guid CategoryId, Guid TatamiId, List<Guid> AthleteIds)>
        SeedAsync(AppDbContext ctx, int athleteCount)
    {
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t = await tStore.CreateAsync("Overview T", new DateOnly(2026, 6, 1), "V", "O", CancellationToken.None);

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club = await clubStore.CreateAsync(t.Id, "JC Overview", null, null, null, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var ids = new List<Guid>();
        for (int i = 0; i < athleteCount; i++)
        {
            var a = await athleteStore.CreateAsync(
                t.Id, club!.Id, $"F{i:D2}", "Fighter", 2000, Gender.Male, null, null, 1, true, CancellationToken.None);
            ids.Add(a!.Id);
        }

        var catStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var cat = await catStore.CreateAsync(
            t.Id, "U18 M -73", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        var tatamiStore = new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance);
        var tatami = await tatamiStore.CreateAsync(t.Id, "Tatami 1", 0, CancellationToken.None);

        return (t.Id, cat!.Id, tatami.Id, ids);
    }

    private static FightRecord CompletedFight(
        Guid tid, Guid cid, Guid white, Guid blue, Guid winner, Guid? tatamiId, DateTimeOffset completedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            TournamentId = tid,
            CategoryId = cid,
            BracketType = FightBracketType.Main.ToString(),
            Round = 1,
            FightNumber = 1,
            Status = FightStatus.Completed.ToString(),
            IsBye = false,
            WhiteAthleteId = white,
            BlueAthleteId = blue,
            WinnerId = winner,
            TatamiId = tatamiId,
            WhiteScore = 10,
            BlueScore = 1,
            WhitePenalties = 0,
            BluePenalties = 1,
            WhiteIpponCount = 1,
            WhiteWazaAriCount = 0,
            WhiteYukoCount = 0,
            BlueIpponCount = 0,
            BlueWazaAriCount = 0,
            BlueYukoCount = 1,
            StartedAtUtc = completedAt.AddMinutes(-3),
            CompletedAtUtc = completedAt,
            CreatedAtUtc = completedAt.AddMinutes(-5),
            UpdatedAtUtc = completedAt,
        };

    [Fact]
    public async Task GetCompletedFights_NoFights_ReturnsEmpty()
    {
        await using var ctx = CreateDbContext();
        var (tid, _, _, _) = await SeedAsync(ctx, 0);
        var svc = new CompletedFightsService(ctx);

        var result = await svc.GetCompletedFightsAsync(tid, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompletedFights_ExcludesByesAndUnfinishedFights()
    {
        await using var ctx = CreateDbContext();
        var (tid, cid, tatamiId, athletes) = await SeedAsync(ctx, 3);
        var now = DateTimeOffset.UtcNow;

        ctx.Fights.AddRange(
            CompletedFight(tid, cid, athletes[0], athletes[1], athletes[0], tatamiId, now),
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid,
                BracketType = FightBracketType.Main.ToString(), Round = 1, FightNumber = 2,
                IsBye = true, Status = FightStatus.Completed.ToString(),
                WhiteAthleteId = athletes[2], BlueAthleteId = null, WinnerId = athletes[2],
                CreatedAtUtc = now, UpdatedAtUtc = now,
            },
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid,
                BracketType = FightBracketType.Main.ToString(), Round = 2, FightNumber = 1,
                IsBye = false, Status = FightStatus.InProgress.ToString(),
                WhiteAthleteId = athletes[0], BlueAthleteId = athletes[2], WinnerId = null,
                CreatedAtUtc = now, UpdatedAtUtc = now,
            });
        await ctx.SaveChangesAsync();

        var svc = new CompletedFightsService(ctx);
        var result = await svc.GetCompletedFightsAsync(tid, CancellationToken.None);

        var entry = Assert.Single(result);
        Assert.Equal("Fighter, F00", entry.WhiteAthleteName);
        Assert.Equal("Fighter, F01", entry.BlueAthleteName);
        Assert.Equal("JC Overview", entry.WhiteClubName);
        Assert.Equal("Tatami 1", entry.TatamiName);
        Assert.Equal("U18 M -73", entry.CategoryName);
    }

    [Fact]
    public async Task GetCompletedFights_ResolvesWinnerSideDurationAndScores()
    {
        await using var ctx = CreateDbContext();
        var (tid, cid, tatamiId, athletes) = await SeedAsync(ctx, 2);
        var now = DateTimeOffset.UtcNow;

        ctx.Fights.Add(CompletedFight(tid, cid, athletes[0], athletes[1], athletes[1], tatamiId, now));
        await ctx.SaveChangesAsync();

        var svc = new CompletedFightsService(ctx);
        var entry = Assert.Single(await svc.GetCompletedFightsAsync(tid, CancellationToken.None));

        Assert.Equal("Blue", entry.WinnerSide);
        Assert.Equal("Fighter, F01", entry.WinnerName);
        Assert.Equal(10, entry.WhiteScore);
        Assert.Equal(1, entry.BlueScore);
        Assert.Equal(1, entry.WhiteIpponCount);
        Assert.Equal(1, entry.BlueYukoCount);
        Assert.Equal(1, entry.BluePenalties);
        Assert.Equal(180, entry.DurationSeconds);
    }

    [Fact]
    public async Task GetCompletedFights_OrdersByCompletionTimeDescending()
    {
        await using var ctx = CreateDbContext();
        var (tid, cid, tatamiId, athletes) = await SeedAsync(ctx, 2);
        var now = DateTimeOffset.UtcNow;

        var older = CompletedFight(tid, cid, athletes[0], athletes[1], athletes[0], tatamiId, now.AddMinutes(-30));
        var newer = CompletedFight(tid, cid, athletes[1], athletes[0], athletes[1], tatamiId, now);
        ctx.Fights.AddRange(older, newer);
        await ctx.SaveChangesAsync();

        var svc = new CompletedFightsService(ctx);
        var result = await svc.GetCompletedFightsAsync(tid, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.Id, result[0].FightId);
        Assert.Equal(older.Id, result[1].FightId);
    }

    [Fact]
    public async Task GetCompletedFights_OtherTournamentFightsAreNotReturned()
    {
        await using var ctx = CreateDbContext();
        var (tid, cid, tatamiId, athletes) = await SeedAsync(ctx, 2);
        var (otherTid, otherCid, otherTatami, otherAthletes) = await SeedAsync(ctx, 2);
        var now = DateTimeOffset.UtcNow;

        ctx.Fights.Add(CompletedFight(tid, cid, athletes[0], athletes[1], athletes[0], tatamiId, now));
        ctx.Fights.Add(CompletedFight(otherTid, otherCid, otherAthletes[0], otherAthletes[1], otherAthletes[0], otherTatami, now));
        await ctx.SaveChangesAsync();

        var svc = new CompletedFightsService(ctx);
        var result = await svc.GetCompletedFightsAsync(tid, CancellationToken.None);

        Assert.Single(result);
    }
}
