using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Hubs;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

public sealed class MatchClockEvaluatorTests
{
    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "clock-evaluator.db");
    }

    private static ServiceProvider BuildProvider(string dbPath)
    {
        var mockHub = new Mock<IHubContext<TournamentHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockProxy = new Mock<IClientProxy>();
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockProxy.Object);
        mockProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddSingleton<IHubContext<TournamentHub>>(mockHub.Object);
        services.AddSingleton<IBracketService>(new Mock<IBracketService>().Object);
        services.AddSingleton<IRankingService>(new Mock<IRankingService>().Object);
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IMatchService, MatchService>();
        return services.BuildServiceProvider();
    }

    private static async Task SeedBaseAsync(AppDbContext context, TournamentRecord tournament, CategoryRecord category)
    {
        context.Tournaments.Add(tournament);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task EvaluateOnceAsync_OsaeKomiAtCap_StopsHoldAndAwardsScore()
    {
        var dbPath = CreateDatabasePath();
        await using (var setupProvider = BuildProvider(dbPath))
        await using (var setupScope = setupProvider.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();

            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            await SeedBaseAsync(
                context,
                new TournamentRecord
                {
                    Id = tournamentId,
                    Name = "Test",
                    Date = new DateOnly(2026, 7, 23),
                    Venue = "Essen",
                    Organizer = "JV",
                    OsaeKomiIpponSeconds = 2,
                    OsaeKomiWazaAriSeconds = 1,
                    OsaeKomiYukoSeconds = 1,
                    OsaeKomiYukoEnabled = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                new CategoryRecord
                {
                    Id = categoryId,
                    TournamentId = tournamentId,
                    Name = "U18",
                    AgeGroup = "U18",
                    Gender = "Male",
                    MatchDurationSeconds = 300,
                    GoldenScoreEnabled = false,
                    GoldenScoreDurationSeconds = 180,
                    IsLocked = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });

            context.Fights.Add(new FightRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                CategoryId = categoryId,
                BracketType = "Main",
                Round = 1,
                FightNumber = 1,
                WhiteAthleteId = Guid.NewGuid(),
                BlueAthleteId = Guid.NewGuid(),
                Status = "InProgress",
                StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-30),
                OsaeKomiSide = "White",
                OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-3),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        await using (var provider = BuildProvider(dbPath))
        {
            var evaluator = new MatchClockEvaluator(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MatchClockEvaluator>.Instance);
            await evaluator.EvaluateOnceAsync(CancellationToken.None);
        }

        await using (var verifyProvider = BuildProvider(dbPath))
        await using (var verifyScope = verifyProvider.CreateAsyncScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fight = await context.Fights.SingleAsync();

            Assert.Null(fight.OsaeKomiSide);
            Assert.Null(fight.OsaeKomiStartedAtUtc);
            Assert.Equal(1, fight.WhiteIpponCount);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task EvaluateOnceAsync_TimeExpiredWithoutOsaeKomi_PausesFight()
    {
        var dbPath = CreateDatabasePath();
        await using (var setupProvider = BuildProvider(dbPath))
        await using (var setupScope = setupProvider.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();

            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            await SeedBaseAsync(
                context,
                new TournamentRecord
                {
                    Id = tournamentId,
                    Name = "Test",
                    Date = new DateOnly(2026, 7, 23),
                    Venue = "Essen",
                    Organizer = "JV",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                new CategoryRecord
                {
                    Id = categoryId,
                    TournamentId = tournamentId,
                    Name = "U18",
                    AgeGroup = "U18",
                    Gender = "Male",
                    MatchDurationSeconds = 1,
                    GoldenScoreEnabled = false,
                    GoldenScoreDurationSeconds = 180,
                    IsLocked = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });

            context.Fights.Add(new FightRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                CategoryId = categoryId,
                BracketType = "Main",
                Round = 1,
                FightNumber = 1,
                WhiteAthleteId = Guid.NewGuid(),
                BlueAthleteId = Guid.NewGuid(),
                Status = "InProgress",
                StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        await using (var provider = BuildProvider(dbPath))
        {
            var evaluator = new MatchClockEvaluator(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MatchClockEvaluator>.Instance);
            await evaluator.EvaluateOnceAsync(CancellationToken.None);
        }

        await using (var verifyProvider = BuildProvider(dbPath))
        await using (var verifyScope = verifyProvider.CreateAsyncScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fight = await context.Fights.SingleAsync();

            Assert.Equal("Paused", fight.Status);
            Assert.NotNull(fight.PausedAtUtc);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task EvaluateOnceAsync_GoldenScoreTieBeforeGoldenLimit_DoesNotPause()
    {
        var dbPath = CreateDatabasePath();
        await using (var setupProvider = BuildProvider(dbPath))
        await using (var setupScope = setupProvider.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();

            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            await SeedBaseAsync(
                context,
                new TournamentRecord
                {
                    Id = tournamentId,
                    Name = "Test",
                    Date = new DateOnly(2026, 7, 23),
                    Venue = "Essen",
                    Organizer = "JV",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                new CategoryRecord
                {
                    Id = categoryId,
                    TournamentId = tournamentId,
                    Name = "U18",
                    AgeGroup = "U18",
                    Gender = "Male",
                    MatchDurationSeconds = 1,
                    GoldenScoreEnabled = true,
                    GoldenScoreDurationSeconds = 10,
                    IsLocked = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });

            context.Fights.Add(new FightRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                CategoryId = categoryId,
                BracketType = "Main",
                Round = 1,
                FightNumber = 1,
                WhiteAthleteId = Guid.NewGuid(),
                BlueAthleteId = Guid.NewGuid(),
                Status = "InProgress",
                StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
                WhiteWazaAriCount = 0,
                BlueWazaAriCount = 0,
                WhiteYukoCount = 0,
                BlueYukoCount = 0,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        await using (var provider = BuildProvider(dbPath))
        {
            var evaluator = new MatchClockEvaluator(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MatchClockEvaluator>.Instance);
            await evaluator.EvaluateOnceAsync(CancellationToken.None);
        }

        await using (var verifyProvider = BuildProvider(dbPath))
        await using (var verifyScope = verifyProvider.CreateAsyncScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fight = await context.Fights.SingleAsync();

            Assert.Equal("InProgress", fight.Status);
            Assert.Null(fight.PausedAtUtc);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task EvaluateOnceAsync_GoldenScoreTieAfterGoldenLimit_PausesFight()
    {
        var dbPath = CreateDatabasePath();
        await using (var setupProvider = BuildProvider(dbPath))
        await using (var setupScope = setupProvider.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();

            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            await SeedBaseAsync(
                context,
                new TournamentRecord
                {
                    Id = tournamentId,
                    Name = "Test",
                    Date = new DateOnly(2026, 7, 23),
                    Venue = "Essen",
                    Organizer = "JV",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                new CategoryRecord
                {
                    Id = categoryId,
                    TournamentId = tournamentId,
                    Name = "U18",
                    AgeGroup = "U18",
                    Gender = "Male",
                    MatchDurationSeconds = 1,
                    GoldenScoreEnabled = true,
                    GoldenScoreDurationSeconds = 10,
                    IsLocked = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });

            context.Fights.Add(new FightRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                CategoryId = categoryId,
                BracketType = "Main",
                Round = 1,
                FightNumber = 1,
                WhiteAthleteId = Guid.NewGuid(),
                BlueAthleteId = Guid.NewGuid(),
                Status = "InProgress",
                StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-15),
                WhiteWazaAriCount = 1,
                BlueWazaAriCount = 1,
                WhiteYukoCount = 0,
                BlueYukoCount = 0,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        await using (var provider = BuildProvider(dbPath))
        {
            var evaluator = new MatchClockEvaluator(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MatchClockEvaluator>.Instance);
            await evaluator.EvaluateOnceAsync(CancellationToken.None);
        }

        await using (var verifyProvider = BuildProvider(dbPath))
        await using (var verifyScope = verifyProvider.CreateAsyncScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fight = await context.Fights.SingleAsync();

            Assert.Equal("Paused", fight.Status);
            Assert.NotNull(fight.PausedAtUtc);
        }
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task EvaluateOnceAsync_RegularTimeExpiredWithActiveOsaeKomi_DoesNotPause()
    {
        var dbPath = CreateDatabasePath();
        await using (var setupProvider = BuildProvider(dbPath))
        await using (var setupScope = setupProvider.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();

            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            await SeedBaseAsync(
                context,
                new TournamentRecord
                {
                    Id = tournamentId,
                    Name = "Test",
                    Date = new DateOnly(2026, 7, 23),
                    Venue = "Essen",
                    Organizer = "JV",
                    OsaeKomiIpponSeconds = 20,
                    OsaeKomiWazaAriSeconds = 10,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                new CategoryRecord
                {
                    Id = categoryId,
                    TournamentId = tournamentId,
                    Name = "U18",
                    AgeGroup = "U18",
                    Gender = "Male",
                    MatchDurationSeconds = 1,
                    GoldenScoreEnabled = false,
                    GoldenScoreDurationSeconds = 180,
                    IsLocked = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });

            context.Fights.Add(new FightRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                CategoryId = categoryId,
                BracketType = "Main",
                Round = 1,
                FightNumber = 1,
                WhiteAthleteId = Guid.NewGuid(),
                BlueAthleteId = Guid.NewGuid(),
                Status = "InProgress",
                StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
                OsaeKomiSide = "Blue",
                OsaeKomiStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        await using (var provider = BuildProvider(dbPath))
        {
            var evaluator = new MatchClockEvaluator(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MatchClockEvaluator>.Instance);
            await evaluator.EvaluateOnceAsync(CancellationToken.None);
        }

        await using (var verifyProvider = BuildProvider(dbPath))
        await using (var verifyScope = verifyProvider.CreateAsyncScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fight = await context.Fights.SingleAsync();

            Assert.Equal("InProgress", fight.Status);
            Assert.Null(fight.PausedAtUtc);
            Assert.Equal("Blue", fight.OsaeKomiSide);
        }
    }
}
