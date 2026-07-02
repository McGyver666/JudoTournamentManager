using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JudoTournamentManagement.Api.Tests;

public sealed class SqliteTournamentStoreTests
{
    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenValidInput_PersistsTournament()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        await using var dbContext = CreateDbContext(databasePath);
        await dbContext.Database.EnsureCreatedAsync();
        var store = new SqliteTournamentStore(dbContext, NullLogger<SqliteTournamentStore>.Instance);

        // Act
        var created = await store.CreateAsync(
            "RWE Cup",
            new DateOnly(2026, 7, 12),
            "Essen",
            "JC Essen",
            CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("RWE Cup", created.Name);
        Assert.Equal(new DateOnly(2026, 7, 12), created.Date);
        Assert.Equal("Essen", created.Venue);
        Assert.Equal("JC Essen", created.Organizer);
        Assert.True(File.Exists(databasePath));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetByIdAsync_WhenStoreIsRecreated_ReturnsPersistedTournament()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        await using (var setupContext = CreateDbContext(databasePath))
        {
            await setupContext.Database.EnsureCreatedAsync();
            var setupStore = new SqliteTournamentStore(setupContext, NullLogger<SqliteTournamentStore>.Instance);
            await setupStore.CreateAsync(
                "Bezirksturnier",
                new DateOnly(2026, 8, 20),
                "Dortmund",
                "Bezirk West",
                CancellationToken.None);
        }

        await using var verificationContext = CreateDbContext(databasePath);
        var verificationStore = new SqliteTournamentStore(verificationContext, NullLogger<SqliteTournamentStore>.Instance);
        var created = (await verificationStore.GetAllAsync(CancellationToken.None)).Single();

        // Act
        var loaded = await verificationStore.GetByIdAsync(created.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenTournamentExists_PersistsUpdatedValues()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        await using (var setupContext = CreateDbContext(databasePath))
        {
            await setupContext.Database.EnsureCreatedAsync();
            var setupStore = new SqliteTournamentStore(setupContext, NullLogger<SqliteTournamentStore>.Instance);
            await setupStore.CreateAsync(
                "Nordcup",
                new DateOnly(2026, 10, 5),
                "Hamburg",
                "Nordverband",
                CancellationToken.None);
        }

        await using (var updateContext = CreateDbContext(databasePath))
        {
            var updateStore = new SqliteTournamentStore(updateContext, NullLogger<SqliteTournamentStore>.Instance);
            var tournamentId = (await updateStore.GetAllAsync(CancellationToken.None)).Single().Id;

            // Act
            var updated = await updateStore.UpdateAsync(
                tournamentId,
                "Nordcup Final",
                new DateOnly(2026, 10, 6),
                "Luebeck",
                "Nordverband",
                CancellationToken.None);

            // Assert
            Assert.True(updated);
        }

        await using var verificationContext = CreateDbContext(databasePath);
        var verificationStore = new SqliteTournamentStore(verificationContext, NullLogger<SqliteTournamentStore>.Instance);
        var loaded = (await verificationStore.GetAllAsync(CancellationToken.None)).Single();

        // Assert
        Assert.Equal("Nordcup Final", loaded.Name);
        Assert.Equal(new DateOnly(2026, 10, 6), loaded.Date);
        Assert.Equal("Luebeck", loaded.Venue);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenTournamentDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        await using var dbContext = CreateDbContext(databasePath);
        await dbContext.Database.EnsureCreatedAsync();
        var store = new SqliteTournamentStore(dbContext, NullLogger<SqliteTournamentStore>.Instance);

        // Act
        var updated = await store.UpdateAsync(
            Guid.NewGuid(),
            "Neu",
            new DateOnly(2026, 9, 1),
            "Koeln",
            "NWJV",
            CancellationToken.None);

        // Assert
        Assert.False(updated);
    }

    private static string CreateDatabasePath()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath, "tournament.db");
    }

    private static AppDbContext CreateDbContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenTournamentWithDependentsExists_RemovesAllData()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();

        var store = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var tournament = await store.CreateAsync("Lösch-Test", new DateOnly(2026, 9, 1), "Berlin", "BJV", CancellationToken.None);

        // Seed dependents
        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club = await clubStore.CreateAsync(tournament.Id, "JC Del", CancellationToken.None);

        var tatamisStore = new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance);
        await tatamisStore.CreateAsync(tournament.Id, "Tatami 1", 0, CancellationToken.None);

        var catStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var cat = await catStore.CreateAsync(tournament.Id, "U18 M -73", "U18", Models.Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var athlete = await athleteStore.CreateAsync(tournament.Id, club!.Id, "Max", "Tester", 2005, Models.Gender.Male, null, 25.0m, false, CancellationToken.None);

        var regStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var reg = await regStore.CreateAsync(tournament.Id, athlete!.Id, 25.0m, null, false, CancellationToken.None);
        await regStore.AssignCategoryAsync(reg!.Id, cat!.Id, CancellationToken.None);

        // Act
        var deleted = await store.DeleteAsync(tournament.Id, CancellationToken.None);

        // Assert
        Assert.True(deleted);
        Assert.Null(await store.GetByIdAsync(tournament.Id, CancellationToken.None));
        Assert.Empty(await clubStore.GetAllAsync(tournament.Id, CancellationToken.None));
        Assert.Empty(await tatamisStore.GetAllAsync(tournament.Id, CancellationToken.None));
        Assert.Empty(await catStore.GetAllAsync(tournament.Id, CancellationToken.None));
        Assert.Empty(await athleteStore.GetAllAsync(tournament.Id, CancellationToken.None));
        Assert.Empty(await regStore.GetDetailedAsync(tournament.Id, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenTournamentDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);

        Assert.False(await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
