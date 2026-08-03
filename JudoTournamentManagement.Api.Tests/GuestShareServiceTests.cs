using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

public sealed class GuestShareServiceTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "guest-share.db");
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
        var mockPresets = new Mock<ICategoryPresetsStore>();
        mockPresets.Setup(p => p.SeedDefaultsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var store = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance, mockPresets.Object);
        var tournament = await store.CreateAsync(
            "Test Turnier", new DateOnly(2026, 9, 1), "Berlin", "BJV", CancellationToken.None);
        return tournament.Id;
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetStateAsync_WhenNoShare_ReportsNotExisting()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var service = new SqliteGuestShareService(ctx);

        var state = await service.GetStateAsync(tid, CancellationToken.None);

        Assert.False(state.Exists);
        Assert.False(state.IsEnabled);
        Assert.False(state.IsActive);
        Assert.Null(state.Token);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task EnableAsync_WhenNoShare_CreatesEnabledTokenWithHighEntropy()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var service = new SqliteGuestShareService(ctx);

        var state = await service.EnableAsync(tid, null, CancellationToken.None);

        Assert.True(state.Exists);
        Assert.True(state.IsEnabled);
        Assert.True(state.IsActive);
        Assert.NotNull(state.Token);
        // 32 random bytes → 43-char base64url string.
        Assert.Equal(43, state.Token!.Length);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task EnableAsync_WhenCalledTwice_KeepsSameToken()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var service = new SqliteGuestShareService(ctx);

        var first = await service.EnableAsync(tid, null, CancellationToken.None);
        await service.DisableAsync(tid, CancellationToken.None);
        var second = await service.EnableAsync(tid, null, CancellationToken.None);

        Assert.Equal(first.Token, second.Token);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DisableAsync_TurnsShareOffButKeepsToken()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var service = new SqliteGuestShareService(ctx);
        var enabled = await service.EnableAsync(tid, null, CancellationToken.None);

        var disabled = await service.DisableAsync(tid, CancellationToken.None);

        Assert.True(disabled.Exists);
        Assert.False(disabled.IsEnabled);
        Assert.False(disabled.IsActive);
        Assert.Equal(enabled.Token, disabled.Token);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task RotateAsync_ReplacesTokenAndInvalidatesOld()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var service = new SqliteGuestShareService(ctx);
        var original = await service.EnableAsync(tid, null, CancellationToken.None);

        var rotated = await service.RotateAsync(tid, null, CancellationToken.None);

        Assert.NotEqual(original.Token, rotated.Token);
        Assert.True(rotated.IsActive);
        Assert.Null(await service.ValidateTokenAsync(original.Token!, CancellationToken.None));
        Assert.Equal(tid, await service.ValidateTokenAsync(rotated.Token!, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task ValidateTokenAsync_WhenEnabled_ReturnsTournamentId()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var service = new SqliteGuestShareService(ctx);
        var state = await service.EnableAsync(tid, null, CancellationToken.None);

        var resolved = await service.ValidateTokenAsync(state.Token!, CancellationToken.None);

        Assert.Equal(tid, resolved);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task ValidateTokenAsync_WhenDisabled_ReturnsNull()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var service = new SqliteGuestShareService(ctx);
        var state = await service.EnableAsync(tid, null, CancellationToken.None);
        await service.DisableAsync(tid, CancellationToken.None);

        var resolved = await service.ValidateTokenAsync(state.Token!, CancellationToken.None);

        Assert.Null(resolved);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task ValidateTokenAsync_WhenExpired_ReturnsNull()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var tid = await SeedTournamentAsync(ctx);
        var service = new SqliteGuestShareService(ctx);
        var state = await service.EnableAsync(tid, DateTimeOffset.UtcNow.AddSeconds(-1), CancellationToken.None);

        Assert.False(state.IsActive);
        Assert.Null(await service.ValidateTokenAsync(state.Token!, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task ValidateTokenAsync_WhenUnknownToken_ReturnsNull()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        await SeedTournamentAsync(ctx);
        var service = new SqliteGuestShareService(ctx);

        Assert.Null(await service.ValidateTokenAsync("does-not-exist", CancellationToken.None));
    }
}
