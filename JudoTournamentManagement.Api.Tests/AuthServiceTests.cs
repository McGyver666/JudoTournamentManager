using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class AuthServiceTests
{
    private static readonly IConfiguration TestConfiguration =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AuthTokenHmacSecret"] = "unit-test-auth-token-hmac-secret-32chars"
            })
            .Build();

    [Fact]
    public async Task BootstrapAdminAsync_WithValidPassword_CreatesAdmin()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);

        var result = await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);

        Assert.True(result.Created);
        Assert.Empty(result.ValidationErrors);
        Assert.Equal(1, await db.UserAccounts.CountAsync());
    }

    [Fact]
    public async Task BootstrapAdminAsync_WithWeakPassword_ReturnsValidationErrors()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);

        var result = await service.BootstrapAdminAsync("admin", "weak", CancellationToken.None);

        Assert.False(result.Created);
        Assert.NotEmpty(result.ValidationErrors);
        Assert.Equal(0, await db.UserAccounts.CountAsync());
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);

        var login = await service.LoginAsync("admin", "SicheresPasswort!123", CancellationToken.None);

        Assert.Equal(LoginStatus.Success, login.Status);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.NotNull(login.ExpiresAtUtc);
    }

    [Fact]
    public async Task LoginAsync_AfterFiveFailures_ReturnsLocked()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);

        for (var i = 0; i < 5; i++)
        {
            await service.LoginAsync("admin", "falsch", CancellationToken.None);
        }

        var locked = await service.LoginAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        Assert.Equal(LoginStatus.Locked, locked.Status);
    }

    [Fact]
    public async Task LogoutAsync_RevokesToken_AndValidationFailsAfterward()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var login = await service.LoginAsync("admin", "SicheresPasswort!123", CancellationToken.None);

        Assert.True(await service.LogoutAsync(login.AccessToken!, CancellationToken.None));
        var user = await service.ValidateTokenAsync(login.AccessToken!, CancellationToken.None);

        Assert.Null(user);
    }

    [Fact]
    public async Task CreateUserAsync_WithValidInput_CreatesAccount()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);

        var result = await service.CreateUserAsync("admin", "operator1", "Operator", "Operator!1234", CancellationToken.None);

        Assert.True(result.Created);
        Assert.NotNull(result.UserId);
        Assert.Equal(2, await db.UserAccounts.CountAsync());
    }

    [Fact]
    public async Task SetUserActiveStateAsync_CannotDeactivateSelf()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);

        var adminId = (await db.UserAccounts.SingleAsync()).Id;
        var result = await service.SetUserActiveStateAsync("admin", adminId, false, CancellationToken.None);

        Assert.False(result.Updated);
        Assert.Equal("SelfDeactivate", result.ErrorCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_RevokesExistingSessions()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var created = await service.CreateUserAsync("admin", "operator1", "Operator", "Operator!1234", CancellationToken.None);

        var login = await service.LoginAsync("operator1", "Operator!1234", CancellationToken.None);
        Assert.Equal(LoginStatus.Success, login.Status);

        var reset = await service.ResetPasswordAsync("admin", created.UserId!.Value, "NeuesPasswort!123", CancellationToken.None);
        Assert.True(reset.Updated);

        var oldTokenUser = await service.ValidateTokenAsync(login.AccessToken!, CancellationToken.None);
        Assert.Null(oldTokenUser);

        var newLogin = await service.LoginAsync("operator1", "NeuesPasswort!123", CancellationToken.None);
        Assert.Equal(LoginStatus.Success, newLogin.Status);
    }

    private static string CreateDatabasePath()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath, "auth.db");
    }

    private static AppDbContext CreateDbContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new AppDbContext(options);
    }
}
