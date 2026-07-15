using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JudoTournamentManagement.Api.Tests;

public sealed class AthletesStoreTests
{
    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "athletes.db");
    }

    private static AppDbContext CreateDbContext(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}").Options;
        return new AppDbContext(options);
    }

    private static async Task<(Guid TournamentId, Guid ClubId)> SeedTournamentAndClubAsync(AppDbContext ctx)
    {
        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t = await tStore.CreateAsync("T", new DateOnly(2026, 1, 1), "V", "O", CancellationToken.None);

        var cStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var c = await cStore.CreateAsync(t.Id, "JC Test", null, null, null, CancellationToken.None);

        return (t.Id, c!.Id);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenValidInput_PersistsAthleteWithAllFields()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid) = await SeedTournamentAndClubAsync(ctx);
        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);

        var created = await store.CreateAsync(
            tid, cid, "Anna", "Schmidt", 2005, Gender.Female, "LIC-001", null, 3, false, CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(tid, created.TournamentId);
        Assert.Equal(cid, created.ClubId);
        Assert.Equal("Anna", created.FirstName);
        Assert.Equal("Schmidt", created.LastName);
        Assert.Equal(2005, created.BirthYear);
        Assert.Equal(Gender.Female, created.Gender);
        Assert.Equal("LIC-001", created.LicenseId);
        Assert.Equal(3, created.Grade);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenDuplicateAndAllowDuplicateFalse_ReturnsNull()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid) = await SeedTournamentAndClubAsync(ctx);
        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);

        await store.CreateAsync(tid, cid, "Tom", "Müller", 2003, Gender.Male, null, null, 1, false, CancellationToken.None);
        var duplicate = await store.CreateAsync(tid, cid, "Tom", "Müller", 2003, Gender.Male, null, null, 1, false, CancellationToken.None);

        Assert.Null(duplicate);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenDuplicateAndAllowDuplicateTrue_CreatesAthlete()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid) = await SeedTournamentAndClubAsync(ctx);
        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);

        await store.CreateAsync(tid, cid, "Tom", "Müller", 2003, Gender.Male, null, null, 1, false, CancellationToken.None);
        var forced = await store.CreateAsync(tid, cid, "Tom", "Müller", 2003, Gender.Male, null, null, 1, true, CancellationToken.None);

        Assert.NotNull(forced);
        Assert.Equal(2, (await store.GetAllAsync(tid, CancellationToken.None)).Count);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateBulkAsync_WhenValidInput_PersistsAllAthletesInOneCall()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid) = await SeedTournamentAndClubAsync(ctx);
        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);

        var created = await store.CreateBulkAsync(
            tid,
            [
                new AthleteImportItem(cid, "Anna", "A", 2005, Gender.Female, "L1", 31.1m, 3),
                new AthleteImportItem(cid, "Berta", "B", 2006, Gender.Female, "L2", 33.4m, 4)
            ],
            allowDuplicate: false,
            CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(2, created.Count);
        Assert.Equal(2, (await store.GetAllAsync(tid, CancellationToken.None)).Count);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateBulkAsync_WhenBatchContainsDuplicateAndAllowDuplicateFalse_ReturnsNull()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid) = await SeedTournamentAndClubAsync(ctx);
        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);

        var created = await store.CreateBulkAsync(
            tid,
            [
                new AthleteImportItem(cid, "Tom", "Müller", 2003, Gender.Male, null, null, 1),
                new AthleteImportItem(cid, "Tom", "Müller", 2003, Gender.Male, null, null, 1)
            ],
            allowDuplicate: false,
            CancellationToken.None);

        Assert.Null(created);
        Assert.Empty(await store.GetAllAsync(tid, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetAllAsync_ReturnsOnlyAthletesForTournament()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t1 = await tStore.CreateAsync("T1", new DateOnly(2026, 1, 1), "A", "B", CancellationToken.None);
        var t2 = await tStore.CreateAsync("T2", new DateOnly(2026, 2, 1), "C", "D", CancellationToken.None);

        var cStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club1 = await cStore.CreateAsync(t1.Id, "JC 1", null, null, null, CancellationToken.None);
        var club2 = await cStore.CreateAsync(t2.Id, "JC 2", null, null, null, CancellationToken.None);

        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        await store.CreateAsync(t1.Id, club1!.Id, "A", "A", 2000, Gender.Male, null, null, 1, true, CancellationToken.None);
        await store.CreateAsync(t1.Id, club1.Id, "B", "B", 2000, Gender.Male, null, null, 1, true, CancellationToken.None);
        await store.CreateAsync(t2.Id, club2!.Id, "C", "C", 2000, Gender.Male, null, null, 1, true, CancellationToken.None);

        var result = await store.GetAllAsync(t1.Id, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(t1.Id, a.TournamentId));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetByIdAsync_WhenStoreIsRecreated_ReturnsAthlete()
    {
        var db = CreateDatabasePath();
        Guid athleteId;

        await using (var setup = CreateDbContext(db))
        {
            await setup.Database.EnsureCreatedAsync();
            var (tid, cid) = await SeedTournamentAndClubAsync(setup);
            var store = new SqliteAthletesStore(setup, NullLogger<SqliteAthletesStore>.Instance);
            var created = await store.CreateAsync(tid, cid, "Eva", "Braun", 2006, Gender.Female, null, null, 1, false, CancellationToken.None);
            athleteId = created!.Id;
        }

        await using var verify = CreateDbContext(db);
        var verifyStore = new SqliteAthletesStore(verify, NullLogger<SqliteAthletesStore>.Instance);
        var loaded = await verifyStore.GetByIdAsync(athleteId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Eva", loaded.FirstName);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenAthleteExists_PersistsChanges()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid) = await SeedTournamentAndClubAsync(ctx);
        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var created = await store.CreateAsync(tid, cid, "Old", "Name", 2000, Gender.Male, null, null, 1, false, CancellationToken.None);

        var updated = await store.UpdateAsync(
            created!.Id, cid, "New", "Name", 2001, Gender.Female, "X99", null, 2, CancellationToken.None);

        Assert.True(updated);
        var loaded = await store.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("New", loaded.FirstName);
        Assert.Equal(2001, loaded.BirthYear);
        Assert.Equal(Gender.Female, loaded.Gender);
        Assert.Equal("X99", loaded.LicenseId);
        Assert.Equal(2, loaded.Grade);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenAthleteDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);

        var updated = await store.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), "X", "Y", 2000, Gender.Male, null, null, 1, CancellationToken.None);

        Assert.False(updated);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenAthleteExists_RemovesAthlete()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, cid) = await SeedTournamentAndClubAsync(ctx);
        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var created = await store.CreateAsync(tid, cid, "Del", "Me", 2000, Gender.Male, null, null, 1, false, CancellationToken.None);

        var deleted = await store.DeleteAsync(created!.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await store.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenAthleteDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);

        Assert.False(await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
