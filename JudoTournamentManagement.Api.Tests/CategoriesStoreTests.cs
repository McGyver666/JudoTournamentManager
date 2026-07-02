using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JudoTournamentManagement.Api.Tests;

public sealed class CategoriesStoreTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "categories.db");
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
        var store = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var tournament = await store.CreateAsync(
            "Test Turnier", new DateOnly(2026, 9, 1), "Berlin", "BJV", CancellationToken.None);
        return tournament.Id;
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenValidInput_PersistsCategory()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);

        var created = await store.CreateAsync(
            tid, "U18 Männer -73 kg", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(tid, created.TournamentId);
        Assert.Equal("U18 Männer -73 kg", created.Name);
        Assert.Equal("U18", created.AgeGroup);
        Assert.Equal(Gender.Male, created.Gender);
        Assert.Equal(73m, created.WeightClassKg);
        Assert.False(created.GoldenScoreEnabled);
        Assert.Equal(180, created.GoldenScoreDurationSeconds);
        Assert.False(created.IsLocked);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenOpenWeightCategory_PersistsNullWeightClass()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);

        var created = await store.CreateAsync(
            tid, "Senioren Frauen Open", "Senioren", Gender.Female, null, null, null, null, 300, false, 180, CancellationToken.None);

        Assert.NotNull(created);
        Assert.Null(created.WeightClassKg);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenDuplicateDefinition_ReturnsNull()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);

        await store.CreateAsync(tid, "U18 M -73", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);
        var duplicate = await store.CreateAsync(tid, "U18 Männer 73kg", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        Assert.Null(duplicate);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenSameDefinitionDifferentGender_Succeeds()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);

        var male = await store.CreateAsync(tid, "U18 M -63", "U18", Gender.Male, 63m, null, null, null, 300, false, 180, CancellationToken.None);
        var female = await store.CreateAsync(tid, "U18 W -63", "U18", Gender.Female, 63m, null, null, null, 300, false, 180, CancellationToken.None);

        Assert.NotNull(male);
        Assert.NotNull(female);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetAllAsync_ReturnsOnlyCategoriesForTournament()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();

        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance);
        var t1 = await tStore.CreateAsync("T1", new DateOnly(2026, 1, 1), "A", "B", CancellationToken.None);
        var t2 = await tStore.CreateAsync("T2", new DateOnly(2026, 2, 1), "C", "D", CancellationToken.None);

        var store = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        await store.CreateAsync(t1.Id, "T1-Cat1", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);
        await store.CreateAsync(t1.Id, "T1-Cat2", "U18", Gender.Female, 57m, null, null, null, 300, false, 180, CancellationToken.None);
        await store.CreateAsync(t2.Id, "T2-Cat1", "U15", Gender.Male, 55m, null, null, null, 300, false, 180, CancellationToken.None);

        var result = await store.GetAllAsync(t1.Id, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(t1.Id, c.TournamentId));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetByIdAsync_WhenStoreIsRecreated_ReturnsCategory()
    {
        var db = CreateDatabasePath();
        Guid categoryId;

        await using (var setup = CreateDbContext(db))
        {
            await setup.Database.EnsureCreatedAsync();
            var tid = await SeedTournamentAsync(setup);
            var store = new SqliteCategoriesStore(setup, NullLogger<SqliteCategoriesStore>.Instance);
            var created = await store.CreateAsync(tid, "U15 W -44", "U15", Gender.Female, 44m, null, null, null, 300, false, 180, CancellationToken.None);
            categoryId = created!.Id;
        }

        await using var verify = CreateDbContext(db);
        var verifyStore = new SqliteCategoriesStore(verify, NullLogger<SqliteCategoriesStore>.Instance);
        var loaded = await verifyStore.GetByIdAsync(categoryId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("U15 W -44", loaded.Name);
        Assert.Equal(Gender.Female, loaded.Gender);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenCategoryExists_PersistsChanges()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var created = await store.CreateAsync(tid, "Alt", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        var updated = await store.UpdateAsync(
            created!.Id, "Neu", "U18", Gender.Male, 66m, null, null, "Regelwerk X", 300, false, 180, CancellationToken.None);

        Assert.True(updated);
        var loaded = await store.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("Neu", loaded.Name);
        Assert.Equal(66m, loaded.WeightClassKg);
        Assert.Equal("Regelwerk X", loaded.RulesetNotes);
        Assert.False(loaded.GoldenScoreEnabled);
        Assert.Equal(180, loaded.GoldenScoreDurationSeconds);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateAsync_WhenCategoryDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);

        var updated = await store.UpdateAsync(
            Guid.NewGuid(), "X", "U18", Gender.Male, null, null, null, null, 300, false, 180, CancellationToken.None);

        Assert.False(updated);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenCategoryExists_RemovesCategory()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var store = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var created = await store.CreateAsync(tid, "Del", "U12", Gender.Female, 36m, null, null, null, 300, false, 180, CancellationToken.None);

        var deleted = await store.DeleteAsync(created!.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await store.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenCategoryDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);

        var deleted = await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(deleted);
    }
}
