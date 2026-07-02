using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JudoTournamentManagement.Api.Tests;

public sealed class TatamisStoreTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "tatamis.db");
    }

    private static AppDbContext CreateDbContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedTournamentAsync(AppDbContext ctx)
    {
        var tournamentStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var tournament = await tournamentStore.CreateAsync(
            "Test Turnier", new DateOnly(2026, 9, 1), "Köln", "NWJV", CancellationToken.None);
        return tournament.Id;
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenValidInput_PersistsTatami()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tournamentId = await SeedTournamentAsync(ctx);
        var store = new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance);

        var created = await store.CreateAsync(tournamentId, "Tatami 1", displayOrder: 0, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(tournamentId, created.TournamentId);
        Assert.Equal("Tatami 1", created.Name);
        Assert.Equal(0, created.DisplayOrder);
        Assert.True(created.IsActive);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenDisplayOrderIsNull_AutoAssignsNextOrder()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tournamentId = await SeedTournamentAsync(ctx);
        var store = new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance);

        var first = await store.CreateAsync(tournamentId, "Tatami 1", displayOrder: null, CancellationToken.None);
        var second = await store.CreateAsync(tournamentId, "Tatami 2", displayOrder: null, CancellationToken.None);

        Assert.Equal(0, first.DisplayOrder);
        Assert.Equal(1, second.DisplayOrder);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetAllAsync_ReturnsOnlyTatamisForTournament()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();

        var tournamentStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t1 = await tournamentStore.CreateAsync("T1", new DateOnly(2026, 1, 1), "A", "B", CancellationToken.None);
        var t2 = await tournamentStore.CreateAsync("T2", new DateOnly(2026, 2, 1), "C", "D", CancellationToken.None);

        var store = new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance);
        await store.CreateAsync(t1.Id, "T1-Tatami 1", null, CancellationToken.None);
        await store.CreateAsync(t1.Id, "T1-Tatami 2", null, CancellationToken.None);
        await store.CreateAsync(t2.Id, "T2-Tatami 1", null, CancellationToken.None);

        var result = await store.GetAllAsync(t1.Id, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(t1.Id, t.TournamentId));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetByIdAsync_WhenStoreIsRecreated_ReturnsTatami()
    {
        var db = CreateDatabasePath();
        Guid tatamisId;

        await using (var setup = CreateDbContext(db))
        {
            await setup.Database.EnsureCreatedAsync();
            var tid = await SeedTournamentAsync(setup);
            var store = new SqliteTatamisStore(setup, NullLogger<SqliteTatamisStore>.Instance);
            var created = await store.CreateAsync(tid, "Tatami A", 0, CancellationToken.None);
            tatamisId = created.Id;
        }

        await using var verify = CreateDbContext(db);
        var verifyStore = new SqliteTatamisStore(verify, NullLogger<SqliteTatamisStore>.Instance);
        var loaded = await verifyStore.GetByIdAsync(tatamisId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Tatami A", loaded.Name);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenTatamisExists_PersistsChanges()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance);
        var created = await store.CreateAsync(tid, "Tatami Alt", 0, CancellationToken.None);

        var updated = await store.UpdateAsync(created.Id, "Tatami Neu", 5, isActive: false, CancellationToken.None);

        Assert.True(updated);
        var loaded = await store.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("Tatami Neu", loaded.Name);
        Assert.Equal(5, loaded.DisplayOrder);
        Assert.False(loaded.IsActive);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenTatamisDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance);

        var updated = await store.UpdateAsync(Guid.NewGuid(), "X", 0, true, CancellationToken.None);

        Assert.False(updated);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenTatamisExists_RemovesTatami()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance);
        var created = await store.CreateAsync(tid, "Tatami X", 0, CancellationToken.None);

        var deleted = await store.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await store.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenTatamisDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteTatamisStore(ctx, NullLogger<SqliteTatamisStore>.Instance);

        var deleted = await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(deleted);
    }
}
