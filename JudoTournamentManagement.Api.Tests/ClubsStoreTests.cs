using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JudoTournamentManagement.Api.Tests;

public sealed class ClubsStoreTests
{
    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "clubs.db");
    }

    private static AppDbContext CreateDbContext(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}").Options;
        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedTournamentAsync(AppDbContext ctx)
    {
        var store = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t = await store.CreateAsync("T", new DateOnly(2026, 1, 1), "V", "O", CancellationToken.None);
        return t.Id;
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenValidInput_PersistsClub()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);

        var created = await store.CreateAsync(tid, "JC Essen", CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(tid, created.TournamentId);
        Assert.Equal("JC Essen", created.Name);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenDuplicateName_ReturnsNull()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);

        await store.CreateAsync(tid, "JC Essen", CancellationToken.None);
        var duplicate = await store.CreateAsync(tid, "JC Essen", CancellationToken.None);

        Assert.Null(duplicate);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenSameNameDifferentTournament_Succeeds()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t1 = await tStore.CreateAsync("T1", new DateOnly(2026, 1, 1), "A", "B", CancellationToken.None);
        var t2 = await tStore.CreateAsync("T2", new DateOnly(2026, 2, 1), "C", "D", CancellationToken.None);

        var store = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var c1 = await store.CreateAsync(t1.Id, "JC Köln", CancellationToken.None);
        var c2 = await store.CreateAsync(t2.Id, "JC Köln", CancellationToken.None);

        Assert.NotNull(c1);
        Assert.NotNull(c2);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetAllAsync_ReturnsOnlyClubsForTournament()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t1 = await tStore.CreateAsync("T1", new DateOnly(2026, 1, 1), "A", "B", CancellationToken.None);
        var t2 = await tStore.CreateAsync("T2", new DateOnly(2026, 2, 1), "C", "D", CancellationToken.None);

        var store = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        await store.CreateAsync(t1.Id, "Club A", CancellationToken.None);
        await store.CreateAsync(t1.Id, "Club B", CancellationToken.None);
        await store.CreateAsync(t2.Id, "Club C", CancellationToken.None);

        var result = await store.GetAllAsync(t1.Id, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(t1.Id, c.TournamentId));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetByIdAsync_WhenStoreIsRecreated_ReturnsClub()
    {
        var db = CreateDatabasePath();
        Guid clubId;

        await using (var setup = CreateDbContext(db))
        {
            await setup.Database.EnsureCreatedAsync();
            var tid = await SeedTournamentAsync(setup);
            var store = new SqliteClubsStore(setup, NullLogger<SqliteClubsStore>.Instance);
            var created = await store.CreateAsync(tid, "JC Hamburg", CancellationToken.None);
            clubId = created!.Id;
        }

        await using var verify = CreateDbContext(db);
        var verifyStore = new SqliteClubsStore(verify, NullLogger<SqliteClubsStore>.Instance);
        var loaded = await verifyStore.GetByIdAsync(clubId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("JC Hamburg", loaded.Name);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenClubExists_PersistsChange()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var created = await store.CreateAsync(tid, "Alt", CancellationToken.None);

        var updated = await store.UpdateAsync(created!.Id, "Neu", CancellationToken.None);

        Assert.True(updated);
        var loaded = await store.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.Equal("Neu", loaded!.Name);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenClubDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);

        var updated = await store.UpdateAsync(Guid.NewGuid(), "X", CancellationToken.None);

        Assert.False(updated);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenClubExists_RemovesClub()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var created = await store.CreateAsync(tid, "Zu Löschen", CancellationToken.None);

        var deleted = await store.DeleteAsync(created!.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await store.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenClubDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);

        Assert.False(await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task HasAthletesAsync_WhenAthleteExists_ReturnsTrue()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club = await clubStore.CreateAsync(tid, "JC X", CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        await athleteStore.CreateAsync(
            tid, club!.Id, "Max", "Mustermann", 2005, Models.Gender.Male, null, null, true, CancellationToken.None);

        Assert.True(await clubStore.HasAthletesAsync(club.Id, CancellationToken.None));
    }
}
