using ShiaiManager.Api.Data;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ShiaiManager.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class TournamentOverviewStatsServiceTests
{
    [Fact]
    public async Task GetAsync_ComputesWholeTournamentStatsAndExcludesByes()
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        var tournamentId = Guid.NewGuid();
        var otherTournamentId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var clubOneId = Guid.NewGuid();
        var clubTwoId = Guid.NewGuid();
        var athleteOneId = Guid.NewGuid();
        var athleteTwoId = Guid.NewGuid();
        var athleteThreeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Tournaments.AddRange(
            new TournamentRecord
            {
                Id = tournamentId,
                Name = "Overview-Turnier",
                Date = new DateOnly(2026, 9, 7),
                Venue = "Sporthalle",
                Organizer = "Verein",
                AccentSideColor = "Blue",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            },
            new TournamentRecord
            {
                Id = otherTournamentId,
                Name = "Anderes Turnier",
                Date = new DateOnly(2026, 9, 7),
                Venue = "Sporthalle",
                Organizer = "Verein",
                AccentSideColor = "Blue",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        context.Clubs.AddRange(
            new ClubRecord { Id = clubOneId, TournamentId = tournamentId, Name = "JC Eins", CreatedAtUtc = now, UpdatedAtUtc = now },
            new ClubRecord { Id = clubTwoId, TournamentId = tournamentId, Name = "JC Zwei", CreatedAtUtc = now, UpdatedAtUtc = now },
            new ClubRecord { Id = Guid.NewGuid(), TournamentId = otherTournamentId, Name = "Fremder Verein", CreatedAtUtc = now, UpdatedAtUtc = now });
        context.Categories.Add(new CategoryRecord
        {
            Id = categoryId,
            TournamentId = tournamentId,
            Name = "U18 -73 kg",
            AgeGroup = "U18",
            Gender = "Male",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        context.Athletes.AddRange(
            Athlete(athleteOneId, tournamentId, clubOneId, "Ada", "Eins", now),
            Athlete(athleteTwoId, tournamentId, clubTwoId, "Ben", "Zwei", now),
            Athlete(athleteThreeId, tournamentId, clubOneId, "Cem", "Drei", now));
        context.Registrations.AddRange(
            Registration(tournamentId, athleteOneId, categoryId, now),
            Registration(tournamentId, athleteTwoId, categoryId, now),
            Registration(tournamentId, athleteThreeId, categoryId, now));

        context.Fights.AddRange(
            Fight(tournamentId, categoryId, athleteOneId, athleteTwoId, FightStatus.Completed, false, now.AddMinutes(-2), now, 1, 0),
            Fight(tournamentId, categoryId, athleteTwoId, athleteThreeId, FightStatus.Completed, false, now.AddMinutes(-4), now.AddMinutes(-1), 0, 2),
            Fight(tournamentId, categoryId, athleteOneId, null, FightStatus.Completed, true, null, now, 8, 0),
            Fight(tournamentId, categoryId, athleteOneId, athleteThreeId, FightStatus.Pending, false, null, null, 0, 0),
            Fight(otherTournamentId, categoryId, athleteOneId, athleteTwoId, FightStatus.Completed, false, now.AddMinutes(-4), now.AddMinutes(-2), 4, 0));
        await context.SaveChangesAsync();

        var service = new TournamentOverviewStatsService(context);

        var result = await service.GetAsync(tournamentId, CancellationToken.None);

        Assert.Equal(3, result.RegisteredAthletes);
        Assert.Equal(2, result.ClubCount);
        Assert.Equal(1, result.CategoryCount);
        Assert.Equal(2, result.FightsCompleted);
        Assert.Equal(3, result.FightsTotal);
        Assert.Equal(150d, result.AverageFightDurationSeconds);
        Assert.Equal(3, result.IpponCount);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullAverageWhenNoRealFightHasStarted()
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        var tournamentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        context.Tournaments.Add(new TournamentRecord
        {
            Id = tournamentId,
            Name = "Leeres Turnier",
            Date = new DateOnly(2026, 9, 7),
            Venue = "Sporthalle",
            Organizer = "Verein",
            AccentSideColor = "Blue",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await context.SaveChangesAsync();

        var service = new TournamentOverviewStatsService(context);

        var result = await service.GetAsync(tournamentId, CancellationToken.None);

        Assert.Null(result.AverageFightDurationSeconds);
        Assert.Equal(0, result.FightsCompleted);
        Assert.Equal(0, result.FightsTotal);
        Assert.Equal(0, result.IpponCount);
    }

    private static AppDbContext CreateDbContext()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ShiaiOverviewStatsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "overview.db")}")
            .Options);
    }

    private static AthleteRecord Athlete(Guid id, Guid tournamentId, Guid clubId, string firstName, string lastName, DateTimeOffset now) => new()
    {
        Id = id,
        TournamentId = tournamentId,
        ClubId = clubId,
        FirstName = firstName,
        LastName = lastName,
        BirthYear = 2010,
        Gender = "Male",
        Grade = 1,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static RegistrationRecord Registration(Guid tournamentId, Guid athleteId, Guid categoryId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        TournamentId = tournamentId,
        AthleteId = athleteId,
        CategoryId = categoryId,
        CreatedAtUtc = now,
    };

    private static FightRecord Fight(
        Guid tournamentId,
        Guid categoryId,
        Guid whiteAthleteId,
        Guid? blueAthleteId,
        FightStatus status,
        bool isBye,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc,
        int whiteIpponCount,
        int blueIpponCount) => new()
    {
        Id = Guid.NewGuid(),
        TournamentId = tournamentId,
        CategoryId = categoryId,
        BracketType = FightBracketType.Main.ToString(),
        Round = 1,
        FightNumber = 1,
        WhiteAthleteId = whiteAthleteId,
        BlueAthleteId = blueAthleteId,
        Status = status.ToString(),
        IsBye = isBye,
        WhiteIpponCount = whiteIpponCount,
        BlueIpponCount = blueIpponCount,
        StartedAtUtc = startedAtUtc,
        CompletedAtUtc = completedAtUtc,
        CreatedAtUtc = startedAtUtc ?? completedAtUtc ?? DateTimeOffset.UtcNow,
        UpdatedAtUtc = completedAtUtc ?? startedAtUtc ?? DateTimeOffset.UtcNow,
    };
}
