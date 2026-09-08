using ShiaiManager.Api.Data;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ShiaiManager.Api.Tests;

/// <summary>
/// Tests SQLite persistence for team-matchday configuration.
/// </summary>
[Trait("Category", "UnitTest")]
public sealed class SqliteTeamMatchdayStoreTests
{
    [Fact]
    public async Task AddTeamAndConfirmWeighIn_ForTeamMatchday_PersistsConfiguration()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        var tournamentId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        await SeedTeamMatchdayAsync(dbContext, tournamentId, clubId, athleteId);
        var store = new SqliteTeamMatchdayStore(dbContext, new TeamMatchdayRules());

        // Act
        var team = await store.AddTeamAsync(tournamentId, clubId, "JC Nord I", CancellationToken.None);
        var weighIn = await store.ConfirmWeighInAsync(tournamentId, athleteId, 65.4m, CancellationToken.None);
        var matchday = await store.GetAsync(tournamentId, CancellationToken.None);

        // Assert
        Assert.NotNull(team);
        Assert.Equal("JC Nord I", team!.Name);
        Assert.NotNull(weighIn);
        Assert.Equal(65.4m, weighIn!.WeightKg);
        Assert.NotNull(matchday);
        Assert.Single(matchday!.Teams);
        Assert.Single(matchday.WeighIns);
    }

    [Fact]
    public async Task DrawOrderAndCreateEncounter_ForConfiguredTeams_PersistsBoth()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        var tournamentId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        await SeedTeamMatchdayAsync(dbContext, tournamentId, clubAId, Guid.NewGuid());
        dbContext.Clubs.Add(new ClubRecord
        {
            Id = clubBId,
            TournamentId = tournamentId,
            Name = "JC Sud",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var store = new SqliteTeamMatchdayStore(dbContext, new TeamMatchdayRules());
        var home = await store.AddTeamAsync(tournamentId, clubAId, "JC Nord I", CancellationToken.None);
        var away = await store.AddTeamAsync(tournamentId, clubBId, "JC Sud I", CancellationToken.None);

        // Act
        var order = await store.DrawWeightClassOrderAsync(tournamentId, CancellationToken.None);
        var encounter = await store.CreateEncounterAsync(tournamentId, home!.Id, away!.Id, null, CancellationToken.None);
        var matchday = await store.GetAsync(tournamentId, CancellationToken.None);

        // Assert
        Assert.NotNull(order);
        Assert.Equal(5, order!.Count);
        Assert.Equal(5, order.Distinct().Count());
        Assert.NotNull(encounter);
        Assert.NotNull(matchday);
        Assert.Single(matchday!.Encounters);
        Assert.Equal(home.Id, matchday.Encounters[0].HomeTeamId);
        Assert.Equal(away.Id, matchday.Encounters[0].AwayTeamId);
    }

    private static AppDbContext CreateDbContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
    }

    private static async Task SeedTeamMatchdayAsync(
        AppDbContext dbContext,
        Guid tournamentId,
        Guid clubId,
        Guid athleteId)
    {
        var now = DateTimeOffset.UtcNow;
        dbContext.Tournaments.Add(new TournamentRecord
        {
            Id = tournamentId,
            Name = "Landesliga Kampftag",
            Date = new DateOnly(2026, 9, 19),
            Venue = "Halle Nord",
            Organizer = "JC Nord",
            CompetitionMode = CompetitionMode.TeamMatchday.ToString(),
            TeamMatchdayProfile = TeamMatchdayProfile.SeniorMen.ToString(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Clubs.Add(new ClubRecord
        {
            Id = clubId,
            TournamentId = tournamentId,
            Name = "JC Nord",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Athletes.Add(new AthleteRecord
        {
            Id = athleteId,
            TournamentId = tournamentId,
            ClubId = clubId,
            FirstName = "Max",
            LastName = "Mustermann",
            BirthYear = 1999,
            Gender = Gender.Male.ToString(),
            Grade = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
    }
}