using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class RankingServiceTests
{
    // ─── Infrastructure helpers ───────────────────────────────────────────────

    private static AppDbContext CreateDbContext()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoRankingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ranking.db");
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}").Options;
        return new AppDbContext(opts);
    }

    private static RankingService CreateService(AppDbContext ctx) => new(ctx);

    /// <summary>
    /// Seeds a tournament + club + athletes + category and returns their IDs.
    /// </summary>
    private static async Task<(Guid TournamentId, Guid CategoryId, Guid ClubId, List<Guid> AthleteIds)>
        SeedAsync(AppDbContext ctx, int athleteCount)
    {
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t = await tStore.CreateAsync("Ranking T", new DateOnly(2026, 6, 1), "V", "O", CancellationToken.None);

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club = await clubStore.CreateAsync(t.Id, "JC Ranking", null, null, null, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var ids = new List<Guid>();
        for (int i = 0; i < athleteCount; i++)
        {
            var a = await athleteStore.CreateAsync(
                t.Id, club!.Id, $"R{i:D2}", "Ranker", 2000, Gender.Male, null, null, 1, true, CancellationToken.None);
            ids.Add(a!.Id);
        }

        var catStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var cat = await catStore.CreateAsync(
            t.Id, "U18 M -73", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        var regStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        foreach (var aid in ids)
        {
            var reg = await regStore.CreateAsync(t.Id, aid, 25.0m, null, false, CancellationToken.None);
            await regStore.AssignCategoryAsync(reg!.Id, cat!.Id, CancellationToken.None);
        }

        return (t.Id, cat!.Id, club!.Id, ids);
    }

    // ─── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCategoryRankings_EmptyCategory_ReturnsEmpty()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var (tid, cid, _, _) = await SeedAsync(ctx, 2);

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCategoryRankings_CompletedFinal_ReturnsGoldAndSilver()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (tid, cid, _, athletes) = await SeedAsync(ctx, 2);
        var (red, blue) = (athletes[0], athletes[1]);

        // Insert a completed final fight (Main, Round=1).
        var fight = new FightRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tid,
            CategoryId = cid,
            Round = 1,
            BracketType = FightBracketType.Main.ToString(),
            Status = FightStatus.Completed.ToString(),
            IsBye = false,
            WhiteAthleteId = red,
            BlueAthleteId = blue,
            WinnerId = red,
            WhiteScore = 10,
            BlueScore = 0,
            WhitePenalties = 0,
            BluePenalties = 0,
            TatamiId = null,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        ctx.Fights.Add(fight);
        await ctx.SaveChangesAsync();

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Place);
        Assert.Equal(red, result[0].AthleteId);
        Assert.Equal(2, result[1].Place);
        Assert.Equal(blue, result[1].AthleteId);
    }

    [Fact]
    public async Task GetCategoryRankings_SingleAthleteByeFight_ReturnsGoldOnly()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (tid, cid, _, athletes) = await SeedAsync(ctx, 1);
        var athlete = athletes[0];

        ctx.Fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tid,
            CategoryId = cid,
            Round = 1,
            BracketType = FightBracketType.Main.ToString(),
            Status = FightStatus.Completed.ToString(),
            IsBye = true,
            WhiteAthleteId = athlete,
            BlueAthleteId = null,
            WinnerId = athlete,
            WhiteScore = 0,
            BlueScore = 0,
            WhitePenalties = 0,
            BluePenalties = 0,
            TatamiId = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        var entry = Assert.Single(result);
        Assert.Equal(1, entry.Place);
        Assert.Equal(athlete, entry.AthleteId);
    }

    [Fact]
    public async Task GetCategoryRankings_RoundRobinWithByesAndNoCompletedRealFights_ReturnsEmpty()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (tid, cid, _, athletes) = await SeedAsync(ctx, 3);
        var (a1, a2, a3) = (athletes[0], athletes[1], athletes[2]);

        var now = DateTimeOffset.UtcNow;

        // Round-robin schedule for 3 athletes contains one bye fight per round.
        // All real fights are still pending, so no ranking should be produced.
        ctx.Fights.AddRange(
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid,
                BracketType = FightBracketType.Main.ToString(), Round = 1, FightNumber = 1,
                IsBye = true, Status = FightStatus.Completed.ToString(),
                WhiteAthleteId = a1, BlueAthleteId = null, WinnerId = a1,
                CreatedAtUtc = now, UpdatedAtUtc = now,
            },
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid,
                BracketType = FightBracketType.Main.ToString(), Round = 1, FightNumber = 2,
                IsBye = false, Status = FightStatus.Pending.ToString(),
                WhiteAthleteId = a2, BlueAthleteId = a3, WinnerId = null,
                CreatedAtUtc = now, UpdatedAtUtc = now,
            },
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid,
                BracketType = FightBracketType.Main.ToString(), Round = 2, FightNumber = 1,
                IsBye = false, Status = FightStatus.Pending.ToString(),
                WhiteAthleteId = a1, BlueAthleteId = a3, WinnerId = null,
                CreatedAtUtc = now, UpdatedAtUtc = now,
            },
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid,
                BracketType = FightBracketType.Main.ToString(), Round = 2, FightNumber = 2,
                IsBye = true, Status = FightStatus.Completed.ToString(),
                WhiteAthleteId = null, BlueAthleteId = a2, WinnerId = a2,
                CreatedAtUtc = now, UpdatedAtUtc = now,
            },
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid,
                BracketType = FightBracketType.Main.ToString(), Round = 3, FightNumber = 1,
                IsBye = false, Status = FightStatus.Pending.ToString(),
                WhiteAthleteId = a1, BlueAthleteId = a2, WinnerId = null,
                CreatedAtUtc = now, UpdatedAtUtc = now,
            },
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid,
                BracketType = FightBracketType.Main.ToString(), Round = 3, FightNumber = 2,
                IsBye = true, Status = FightStatus.Completed.ToString(),
                WhiteAthleteId = a3, BlueAthleteId = null, WinnerId = a3,
                CreatedAtUtc = now, UpdatedAtUtc = now,
            });

        await ctx.SaveChangesAsync();

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCategoryRankings_RepechageBronze_AddsThirdPlace()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (tid, cid, _, athletes) = await SeedAsync(ctx, 4);
        var (r0, b0, r1, b1) = (athletes[0], athletes[1], athletes[2], athletes[3]);

        // Completed main final: r0 beats b0
        ctx.Fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid, Round = 2,
            BracketType = FightBracketType.Main.ToString(),
            Status = FightStatus.Completed.ToString(), IsBye = false,
            WhiteAthleteId = r0, BlueAthleteId = b0, WinnerId = r0,
            WhiteScore = 10, BlueScore = 0, WhitePenalties = 0, BluePenalties = 0,
            TatamiId = null, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        // Completed repechage bronze fight: r1 beats b1
        ctx.Fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid, Round = 1,
            BracketType = FightBracketType.Repechage.ToString(),
            Status = FightStatus.Completed.ToString(), IsBye = false,
            WhiteAthleteId = r1, BlueAthleteId = b1, WinnerId = r1,
            WhiteScore = 7, BlueScore = 0, WhitePenalties = 0, BluePenalties = 0,
            TatamiId = null, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        await ctx.SaveChangesAsync();

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result.Single(e => e.AthleteId == r0).Place);
        Assert.Equal(2, result.Single(e => e.AthleteId == b0).Place);
        Assert.Equal(3, result.Single(e => e.AthleteId == r1).Place);
    }

    [Fact]
    public async Task GetCategoryRankings_RepechageBronzeBye_IncludesBothThirdPlaces()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (tid, cid, _, athletes) = await SeedAsync(ctx, 5);
        var (gold, silver, bronzeOne, bronzeTwo, unused) = (athletes[0], athletes[1], athletes[2], athletes[3], athletes[4]);

        ctx.Fights.AddRange(
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid, Round = 3,
                BracketType = FightBracketType.Main.ToString(),
                Status = FightStatus.Completed.ToString(), IsBye = false,
                WhiteAthleteId = gold, BlueAthleteId = silver, WinnerId = gold,
                WhiteScore = 10, BlueScore = 0, WhitePenalties = 0, BluePenalties = 0,
                TatamiId = null, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
            },
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid, Round = 2,
                BracketType = FightBracketType.Repechage.ToString(),
                Status = FightStatus.Completed.ToString(), IsBye = false,
                WhiteAthleteId = bronzeOne, BlueAthleteId = unused, WinnerId = bronzeOne,
                WhiteScore = 7, BlueScore = 0, WhitePenalties = 0, BluePenalties = 0,
                TatamiId = null, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
            },
            new FightRecord
            {
                Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid, Round = 2,
                BracketType = FightBracketType.Repechage.ToString(),
                Status = FightStatus.Completed.ToString(), IsBye = true,
                WhiteAthleteId = bronzeTwo, BlueAthleteId = null, WinnerId = bronzeTwo,
                WhiteScore = 0, BlueScore = 0, WhitePenalties = 0, BluePenalties = 0,
                TatamiId = null, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

        await ctx.SaveChangesAsync();

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        Assert.Equal(4, result.Count);
        Assert.Equal(1, result.Single(e => e.AthleteId == gold).Place);
        Assert.Equal(2, result.Single(e => e.AthleteId == silver).Place);
        Assert.Equal(3, result.Single(e => e.AthleteId == bronzeOne).Place);
        Assert.Equal(3, result.Single(e => e.AthleteId == bronzeTwo).Place);
    }

    [Fact]
    public async Task GetCategoryRankings_IncompleteBracket_ReturnsPartialRanking()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (tid, cid, _, athletes) = await SeedAsync(ctx, 2);
        var (red, blue) = (athletes[0], athletes[1]);

        // Fight still in progress — no winner yet.
        ctx.Fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cid, Round = 1,
            BracketType = FightBracketType.Main.ToString(),
            Status = FightStatus.InProgress.ToString(), IsBye = false,
            WhiteAthleteId = red, BlueAthleteId = blue, WinnerId = null,
            WhiteScore = 0, BlueScore = 0, WhitePenalties = 0, BluePenalties = 0,
            TatamiId = null, StartedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        // No completed fights → no ranked positions.
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCategoryRankings_RoundRobinCompleted_ReturnsTopThreeIncludingBronze()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (tid, cid, _, athleteIds) = await SeedAsync(ctx, 3);

        var category = await ctx.Categories.FirstAsync(c => c.Id == cid);
        category.DrawFormat = BracketFormat.RoundRobin.ToString();

        var a0 = athleteIds[0]; // 2 wins
        var a1 = athleteIds[1]; // 1 win
        var a2 = athleteIds[2]; // 0 wins

        ctx.Fights.AddRange(
            MakeRRFight(tid, cid, a0, a1, winner: a0, fightNumber: 1),
            MakeRRFight(tid, cid, a0, a2, winner: a0, fightNumber: 2),
            MakeRRFight(tid, cid, a1, a2, winner: a1, fightNumber: 3)
        );
        await ctx.SaveChangesAsync();

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal((1, a0), (result[0].Place, result[0].AthleteId));
        Assert.Equal((2, a1), (result[1].Place, result[1].AthleteId));
        Assert.Equal((3, a2), (result[2].Place, result[2].AthleteId));
    }

    [Fact]
    public async Task GetCategoryRankings_RoundRobinWithTwoThirdPlacesEnabled_AwardsTwoBronze()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (tid, cid, _, athleteIds) = await SeedAsync(ctx, 4);

        var category = await ctx.Categories.FirstAsync(c => c.Id == cid);
        category.DrawFormat = BracketFormat.RoundRobin.ToString();

        var tournament = await ctx.Tournaments.FirstAsync(t => t.Id == tid);
        tournament.TwoThirdPlacesInRoundRobin = true;

        var a0 = athleteIds[0]; // 3 wins
        var a1 = athleteIds[1]; // 2 wins
        var a2 = athleteIds[2]; // 1 win
        var a3 = athleteIds[3]; // 0 wins

        ctx.Fights.AddRange(
            MakeRRFight(tid, cid, a0, a1, winner: a0, fightNumber: 1),
            MakeRRFight(tid, cid, a0, a2, winner: a0, fightNumber: 2),
            MakeRRFight(tid, cid, a0, a3, winner: a0, fightNumber: 3),
            MakeRRFight(tid, cid, a1, a2, winner: a1, fightNumber: 4),
            MakeRRFight(tid, cid, a1, a3, winner: a1, fightNumber: 5),
            MakeRRFight(tid, cid, a2, a3, winner: a2, fightNumber: 6)
        );
        await ctx.SaveChangesAsync();

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        Assert.Equal(4, result.Count);
        Assert.Equal((1, a0), (result[0].Place, result[0].AthleteId));
        Assert.Equal((2, a1), (result[1].Place, result[1].AthleteId));
        Assert.Equal((3, a2), (result[2].Place, result[2].AthleteId));
        Assert.Equal((3, a3), (result[3].Place, result[3].AthleteId));
    }

    [Fact]
    public async Task GetCategoryRankings_RoundRobinWithTwoThirdPlacesDisabled_AwardsSingleBronze()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var (tid, cid, _, athleteIds) = await SeedAsync(ctx, 4);

        var category = await ctx.Categories.FirstAsync(c => c.Id == cid);
        category.DrawFormat = BracketFormat.RoundRobin.ToString();

        var a0 = athleteIds[0]; // 3 wins
        var a1 = athleteIds[1]; // 2 wins
        var a2 = athleteIds[2]; // 1 win
        var a3 = athleteIds[3]; // 0 wins

        ctx.Fights.AddRange(
            MakeRRFight(tid, cid, a0, a1, winner: a0, fightNumber: 1),
            MakeRRFight(tid, cid, a0, a2, winner: a0, fightNumber: 2),
            MakeRRFight(tid, cid, a0, a3, winner: a0, fightNumber: 3),
            MakeRRFight(tid, cid, a1, a2, winner: a1, fightNumber: 4),
            MakeRRFight(tid, cid, a1, a3, winner: a1, fightNumber: 5),
            MakeRRFight(tid, cid, a2, a3, winner: a2, fightNumber: 6)
        );
        await ctx.SaveChangesAsync();

        var result = await svc.GetCategoryRankingsAsync(tid, cid, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal((3, a2), (result[2].Place, result[2].AthleteId));
    }

    [Fact]
    public async Task GetMedalTable_AggregatesAcrossCategories()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var tournament = await tStore.CreateAsync("Medal T", new DateOnly(2026, 6, 1), "V", "O", CancellationToken.None);
        var tid = tournament.Id;

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club1 = await clubStore.CreateAsync(tid, "Club Alpha", null, null, null, CancellationToken.None);
        var club2 = await clubStore.CreateAsync(tid, "Club Beta", null, null, null, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var a1 = await athleteStore.CreateAsync(tid, club1!.Id, "Gold", "Alpha", 2000, Gender.Male, null, null, 1, true, CancellationToken.None);
        var a2 = await athleteStore.CreateAsync(tid, club2!.Id, "Silver", "Beta", 2000, Gender.Male, null, null, 1, true, CancellationToken.None);

        var catStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var cat = await catStore.CreateAsync(tid, "Cat A", "U18", Gender.Male, null, null, null, null, 300, false, 180, CancellationToken.None);
        var regStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var r1 = await regStore.CreateAsync(tid, a1!.Id, 25.0m, null, false, CancellationToken.None);
        await regStore.AssignCategoryAsync(r1!.Id, cat!.Id, CancellationToken.None);
        var r2 = await regStore.CreateAsync(tid, a2!.Id, 25.0m, null, false, CancellationToken.None);
        await regStore.AssignCategoryAsync(r2!.Id, cat.Id, CancellationToken.None);

        // Completed final: a1 wins gold, a2 gets silver.
        ctx.Fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(), TournamentId = tid, CategoryId = cat.Id, Round = 1,
            BracketType = FightBracketType.Main.ToString(),
            Status = FightStatus.Completed.ToString(), IsBye = false,
            WhiteAthleteId = a1!.Id, BlueAthleteId = a2!.Id, WinnerId = a1.Id,
            WhiteScore = 10, BlueScore = 0, WhitePenalties = 0, BluePenalties = 0,
            TatamiId = null, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var medals = await svc.GetMedalTableAsync(tid, CancellationToken.None);

        var alpha = medals.FirstOrDefault(m => m.ClubName == "Club Alpha");
        var beta = medals.FirstOrDefault(m => m.ClubName == "Club Beta");

        Assert.NotNull(alpha);
        Assert.NotNull(beta);
        Assert.Equal(1, alpha!.Gold);
        Assert.Equal(0, alpha.Silver);
        Assert.Equal(1, beta!.Silver);
        Assert.Equal(0, beta.Gold);
    }

    [Fact]
    public async Task GetAgeGroupClubScoring_ComputesPerAgeGroupScore_AndFinalStatus()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var tournament = await tStore.CreateAsync("Club Score", new DateOnly(2026, 7, 1), "V", "O", CancellationToken.None);
        var tid = tournament.Id;

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var clubA = await clubStore.CreateAsync(tid, "Judo A", null, null, null, CancellationToken.None);
        var clubB = await clubStore.CreateAsync(tid, "Judo B", null, null, null, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var athleteA = await athleteStore.CreateAsync(tid, clubA!.Id, "Max", "A", 2011, Gender.Male, null, null, 1, true, CancellationToken.None);
        var athleteB = await athleteStore.CreateAsync(tid, clubB!.Id, "Tom", "B", 2011, Gender.Male, null, null, 1, true, CancellationToken.None);

        var categoryStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var category = await categoryStore.CreateAsync(
            tid,
            "U15 -46",
            "U15",
            Gender.Male,
            46m,
            null,
            null,
            null,
            180,
            false,
            60,
            CancellationToken.None);

        var registrationStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var regA = await registrationStore.CreateAsync(tid, athleteA!.Id, 46m, null, false, CancellationToken.None);
        var regB = await registrationStore.CreateAsync(tid, athleteB!.Id, 46m, null, false, CancellationToken.None);
        await registrationStore.AssignCategoryAsync(regA!.Id, category!.Id, CancellationToken.None);
        await registrationStore.AssignCategoryAsync(regB!.Id, category.Id, CancellationToken.None);

        ctx.Fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tid,
            CategoryId = category.Id,
            Round = 1,
            FightNumber = 1,
            BracketType = FightBracketType.Main.ToString(),
            Status = FightStatus.Completed.ToString(),
            IsBye = false,
            WhiteAthleteId = athleteA.Id,
            BlueAthleteId = athleteB.Id,
            WinnerId = athleteA.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var result = await svc.GetAgeGroupClubScoringAsync(tid, CancellationToken.None);

        var u15 = Assert.Single(result.Items);
        Assert.Equal("U15", u15.AgeGroup);
        Assert.Equal("Final", u15.Status);
        Assert.Equal(1, u15.PlannedFights);
        Assert.Equal(1, u15.CompletedFights);

        Assert.Equal(2, u15.Clubs.Count);
        var first = u15.Clubs.First(c => c.ClubName == "Judo A");
        var second = u15.Clubs.First(c => c.ClubName == "Judo B");

        Assert.Equal(1, first.Rank);
        Assert.Equal(7, first.BasePoints);
        Assert.Equal(1, first.Wins);
        Assert.Equal(1, first.Fights);
        Assert.Equal(1m, first.WinRateRaw);
        Assert.Equal(7m, first.ScoreRaw);

        Assert.Equal(2, second.Rank);
        Assert.Equal(5, second.BasePoints);
        Assert.Equal(0, second.Wins);
        Assert.Equal(1, second.Fights);
        Assert.Equal(0m, second.WinRateRaw);
        Assert.Equal(0m, second.ScoreRaw);
    }

    [Fact]
    public async Task GetAgeGroupClubScoring_ByeOnlyCategory_KeepsBasePointsButZeroWinRate()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var tournament = await tStore.CreateAsync("Club Score Bye", new DateOnly(2026, 7, 1), "V", "O", CancellationToken.None);
        var tid = tournament.Id;

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club = await clubStore.CreateAsync(tid, "Judo Solo", null, null, null, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var athlete = await athleteStore.CreateAsync(tid, club!.Id, "Solo", "One", 2012, Gender.Male, null, null, 1, true, CancellationToken.None);

        var categoryStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var category = await categoryStore.CreateAsync(
            tid,
            "U13 -40",
            "U13",
            Gender.Male,
            40m,
            null,
            null,
            null,
            180,
            false,
            60,
            CancellationToken.None);

        var registrationStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var registration = await registrationStore.CreateAsync(tid, athlete!.Id, 40m, null, false, CancellationToken.None);
        await registrationStore.AssignCategoryAsync(registration!.Id, category!.Id, CancellationToken.None);

        ctx.Fights.Add(new FightRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tid,
            CategoryId = category.Id,
            Round = 1,
            FightNumber = 1,
            BracketType = FightBracketType.Main.ToString(),
            Status = FightStatus.Completed.ToString(),
            IsBye = true,
            WhiteAthleteId = athlete.Id,
            BlueAthleteId = null,
            WinnerId = athlete.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var result = await svc.GetAgeGroupClubScoringAsync(tid, CancellationToken.None);

        var row = Assert.Single(Assert.Single(result.Items).Clubs);
        Assert.Equal(7, row.BasePoints);
        Assert.Equal(0, row.Fights);
        Assert.Equal(0, row.Wins);
        Assert.Equal(0m, row.WinRateRaw);
        Assert.Equal(0m, row.ScoreRaw);
    }

    [Fact]
    public async Task GetGlobalClubScoring_UsesCompetitionRankingForSharedPlaces()
    {
        await using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var tournament = await tStore.CreateAsync("Global Score", new DateOnly(2026, 7, 1), "V", "O", CancellationToken.None);
        var tid = tournament.Id;

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var clubA = await clubStore.CreateAsync(tid, "Club A", null, null, null, CancellationToken.None);
        var clubB = await clubStore.CreateAsync(tid, "Club B", null, null, null, CancellationToken.None);
        var clubC = await clubStore.CreateAsync(tid, "Club C", null, null, null, CancellationToken.None);
        await clubStore.CreateAsync(tid, "Club D", null, null, null, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var a1 = await athleteStore.CreateAsync(tid, clubA!.Id, "A", "One", 2010, Gender.Male, null, null, 1, true, CancellationToken.None);
        var b1 = await athleteStore.CreateAsync(tid, clubB!.Id, "B", "One", 2010, Gender.Male, null, null, 1, true, CancellationToken.None);
        var a2 = await athleteStore.CreateAsync(tid, clubA.Id, "A", "Two", 2010, Gender.Male, null, null, 1, true, CancellationToken.None);
        var c1 = await athleteStore.CreateAsync(tid, clubC!.Id, "C", "One", 2010, Gender.Male, null, null, 1, true, CancellationToken.None);

        var categoryStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var cU15 = await categoryStore.CreateAsync(tid, "U15 -50", "U15", Gender.Male, 50m, null, null, null, 180, false, 60, CancellationToken.None);
        var cU18 = await categoryStore.CreateAsync(tid, "U18 -60", "U18", Gender.Male, 60m, null, null, null, 180, false, 60, CancellationToken.None);

        var registrationStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var r1 = await registrationStore.CreateAsync(tid, a1!.Id, 50m, null, false, CancellationToken.None);
        var r2 = await registrationStore.CreateAsync(tid, b1!.Id, 50m, null, false, CancellationToken.None);
        var r3 = await registrationStore.CreateAsync(tid, a2!.Id, 60m, null, false, CancellationToken.None);
        var r4 = await registrationStore.CreateAsync(tid, c1!.Id, 60m, null, false, CancellationToken.None);
        await registrationStore.AssignCategoryAsync(r1!.Id, cU15!.Id, CancellationToken.None);
        await registrationStore.AssignCategoryAsync(r2!.Id, cU15.Id, CancellationToken.None);
        await registrationStore.AssignCategoryAsync(r3!.Id, cU18!.Id, CancellationToken.None);
        await registrationStore.AssignCategoryAsync(r4!.Id, cU18.Id, CancellationToken.None);

        ctx.Fights.AddRange(
            new FightRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tid,
                CategoryId = cU15.Id,
                Round = 1,
                FightNumber = 1,
                BracketType = FightBracketType.Main.ToString(),
                Status = FightStatus.Completed.ToString(),
                IsBye = false,
                WhiteAthleteId = a1.Id,
                BlueAthleteId = b1.Id,
                WinnerId = a1.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            },
            new FightRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tid,
                CategoryId = cU18.Id,
                Round = 1,
                FightNumber = 1,
                BracketType = FightBracketType.Main.ToString(),
                Status = FightStatus.Completed.ToString(),
                IsBye = false,
                WhiteAthleteId = a2.Id,
                BlueAthleteId = c1.Id,
                WinnerId = a2.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        await ctx.SaveChangesAsync();

        var result = await svc.GetGlobalClubScoringAsync(tid, CancellationToken.None);

        Assert.Equal("Final", result.Status);
        Assert.Equal(4, result.Clubs.Count);

        var clubAEntry = result.Clubs.First(c => c.ClubName == "Club A");
        var clubBEntry = result.Clubs.First(c => c.ClubName == "Club B");
        var clubCEntry = result.Clubs.First(c => c.ClubName == "Club C");
        var clubDEntry = result.Clubs.First(c => c.ClubName == "Club D");

        Assert.Equal(1, clubAEntry.Rank);
        Assert.Equal(2, clubBEntry.Rank);
        Assert.True(clubBEntry.IsSharedRank);
        Assert.Equal(2, clubCEntry.Rank);
        Assert.True(clubCEntry.IsSharedRank);
        Assert.Equal(4, clubDEntry.Rank);
        Assert.Equal(0m, clubDEntry.ScoreRaw);
    }

    // ─── Round-robin standings tests ──────────────────────────────────────────

    private static FightRecord MakeRRFight(
        Guid tournamentId, Guid categoryId,
        Guid red, Guid blue,
        Guid winner,
        int redWazaAri = 0, int blueWazaAri = 0,
        int redYuko = 0, int blueYuko = 0,
        int redShidos = 0, int blueShidos = 0,
        int round = 1, int fightNumber = 1,
        int? poolNumber = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new FightRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            CategoryId = categoryId,
            BracketType = FightBracketType.Main.ToString(),
            Round = round,
            FightNumber = fightNumber,
            PoolNumber = poolNumber,
            WhiteAthleteId = red,
            BlueAthleteId = blue,
            WinnerId = winner,
            IsBye = false,
            Status = FightStatus.Completed.ToString(),
            WhiteWazaAriCount = redWazaAri,
            BlueWazaAriCount = blueWazaAri,
            WhiteYukoCount = redYuko,
            BlueYukoCount = blueYuko,
            WhitePenalties = redShidos,
            BluePenalties = blueShidos,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetRoundRobinStandings_ThreeAthletes_RankedByWins()
    {
        await using var ctx = CreateDbContext();
        var (tid, cid, _, athleteIds) = await SeedAsync(ctx, 3);

        var a0 = athleteIds[0]; // 2 wins
        var a1 = athleteIds[1]; // 1 win
        var a2 = athleteIds[2]; // 0 wins

        ctx.Fights.AddRange(
            MakeRRFight(tid, cid, a0, a1, winner: a0, fightNumber: 1),
            MakeRRFight(tid, cid, a0, a2, winner: a0, fightNumber: 2),
            MakeRRFight(tid, cid, a1, a2, winner: a1, fightNumber: 3)
        );
        await ctx.SaveChangesAsync();

        var standings = await CreateService(ctx).GetRoundRobinStandingsAsync(tid, cid, CancellationToken.None);

        Assert.Equal(3, standings.Count);
        Assert.Equal(a0, standings[0].AthleteId);
        Assert.Equal(2, standings[0].Wins);
        Assert.Equal(a1, standings[1].AthleteId);
        Assert.Equal(1, standings[1].Wins);
        Assert.Equal(a2, standings[2].AthleteId);
        Assert.Equal(0, standings[2].Wins);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetRoundRobinStandings_TieBreaksOnWazaAri()
    {
        await using var ctx = CreateDbContext();
        var (tid, cid, _, athleteIds) = await SeedAsync(ctx, 3);

        var a0 = athleteIds[0]; // 1 win, 2 waza-ari
        var a1 = athleteIds[1]; // 1 win, 1 waza-ari
        var a2 = athleteIds[2]; // 1 win, 0 waza-ari (doesn't matter)

        ctx.Fights.AddRange(
            // a0 beats a1 with 2 waza-ari scored
            MakeRRFight(tid, cid, a0, a1, winner: a0, redWazaAri: 2, fightNumber: 1),
            // a1 beats a2 with 1 waza-ari
            MakeRRFight(tid, cid, a1, a2, winner: a1, redWazaAri: 1, fightNumber: 2),
            // a2 beats a0
            MakeRRFight(tid, cid, a2, a0, winner: a2, fightNumber: 3)
        );
        await ctx.SaveChangesAsync();

        var standings = await CreateService(ctx).GetRoundRobinStandingsAsync(tid, cid, CancellationToken.None);

        Assert.Equal(3, standings.Count);
        // Each has 1 win; a0 has most waza-ari scored (2)
        Assert.Equal(a0, standings[0].AthleteId);
        Assert.Equal(2, standings[0].WazaAriScored);
        Assert.Equal(a1, standings[1].AthleteId);
        Assert.Equal(1, standings[1].WazaAriScored);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetRoundRobinStandings_TieBreaksOnShidos_FewerIsHigher()
    {
        await using var ctx = CreateDbContext();
        var (tid, cid, _, athleteIds) = await SeedAsync(ctx, 2);

        var a0 = athleteIds[0]; // wins, receives 1 shido
        var a1 = athleteIds[1]; // loses, receives 3 shidos

        ctx.Fights.Add(MakeRRFight(tid, cid, a0, a1, winner: a0,
            redShidos: 1, blueShidos: 3, fightNumber: 1));
        await ctx.SaveChangesAsync();

        var standings = await CreateService(ctx).GetRoundRobinStandingsAsync(tid, cid, CancellationToken.None);

        Assert.Equal(2, standings.Count);
        Assert.Equal(a0, standings[0].AthleteId); // winner ranked 1st
        Assert.Equal(1, standings[0].Rank);
        Assert.Equal(a1, standings[1].AthleteId);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetRoundRobinStandings_EmptyForCategoryWithNoFights()
    {
        await using var ctx = CreateDbContext();
        var (tid, cid, _, _) = await SeedAsync(ctx, 3);

        var standings = await CreateService(ctx).GetRoundRobinStandingsAsync(tid, cid, CancellationToken.None);

        Assert.Empty(standings);
    }
}
