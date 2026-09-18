using ShiaiManager.Api.Data;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ShiaiManager.Api.Tests;

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
    public async Task SetUserActiveStateAsync_WhenDeactivatingOtherUser_RevokesTargetSessions()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var created = await service.CreateUserAsync("admin", "operator1", "Operator", "Operator!1234", CancellationToken.None);
        var adminLogin = await service.LoginAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var targetLogin = await service.LoginAsync("operator1", "Operator!1234", CancellationToken.None);

        var result = await service.SetUserActiveStateAsync(
            "admin",
            created.UserId!.Value,
            false,
            CancellationToken.None);

        Assert.True(result.Updated);
        Assert.NotNull(await service.ValidateTokenAsync(adminLogin.AccessToken!, CancellationToken.None));
        Assert.Null(await service.ValidateTokenAsync(targetLogin.AccessToken!, CancellationToken.None));
        Assert.Equal(LoginStatus.Inactive, (await service.LoginAsync(
            "operator1",
            "Operator!1234",
            CancellationToken.None)).Status);
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

    [Fact]
    public async Task DeleteUserAsync_RemovesAccountInvalidatesSessionsAndWritesAuditEntry()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var created = await service.CreateUserAsync("admin", "operator1", "Operator", "Operator!1234", CancellationToken.None);
        var login = await service.LoginAsync("operator1", "Operator!1234", CancellationToken.None);

        var result = await service.DeleteUserAsync("admin", created.UserId!.Value, CancellationToken.None);

        Assert.True(result.Deleted);
        Assert.Null(await service.ValidateTokenAsync(login.AccessToken!, CancellationToken.None));
        Assert.False(await db.UserAccounts.AnyAsync(x => x.Id == created.UserId.Value));
        audit.Verify(x => x.LogAsync(
            null,
            "admin",
            "UserDeleted",
            "Auth",
            created.UserId.Value,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_DoesNotDeleteLastAdmin()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var adminId = (await db.UserAccounts.SingleAsync()).Id;

        var result = await service.DeleteUserAsync("operator1", adminId, CancellationToken.None);

        Assert.False(result.Deleted);
        Assert.Equal("LastAdmin", result.ErrorCode);
        Assert.True(await db.UserAccounts.AnyAsync(x => x.Id == adminId));
        audit.Verify(x => x.LogAsync(
            It.IsAny<Guid?>(),
            It.IsAny<string>(),
            "UserDeleted",
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_DoesNotDeleteActor()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var adminId = (await db.UserAccounts.SingleAsync()).Id;

        var result = await service.DeleteUserAsync("admin", adminId, CancellationToken.None);

        Assert.False(result.Deleted);
        Assert.Equal("SelfDelete", result.ErrorCode);
        Assert.True(await db.UserAccounts.AnyAsync(x => x.Id == adminId));
    }

    [Fact]
    public async Task ChangePasswordAsync_KeepsCurrentSession_AndRevokesOtherSessions()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var created = await service.CreateUserAsync("admin", "operator1", "Operator", "Operator!1234", CancellationToken.None);

        var currentLogin = await service.LoginAsync("operator1", "Operator!1234", CancellationToken.None);
        var otherLogin = await service.LoginAsync("operator1", "Operator!1234", CancellationToken.None);

        var changed = await service.ChangePasswordAsync(
            created.UserId!.Value,
            currentLogin.AccessToken!,
            "Operator!1234",
            "NeuesPasswort!123",
            CancellationToken.None);

        Assert.True(changed.Changed);
        Assert.NotNull(await service.ValidateTokenAsync(currentLogin.AccessToken!, CancellationToken.None));
        Assert.Null(await service.ValidateTokenAsync(otherLogin.AccessToken!, CancellationToken.None));
        Assert.Equal(LoginStatus.InvalidCredentials, (await service.LoginAsync("operator1", "Operator!1234", CancellationToken.None)).Status);
        Assert.Equal(LoginStatus.Success, (await service.LoginAsync("operator1", "NeuesPasswort!123", CancellationToken.None)).Status);
        audit.Verify(x => x.LogAsync(
            null,
            "operator1",
            "PasswordChanged",
            "Auth",
            created.UserId.Value,
            It.Is<string?>(details => details == "User=operator1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_DoesNotChangePassword()
    {
        var dbPath = CreateDatabasePath();
        await using var db = CreateDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();

        var audit = new Mock<IAuditLogService>();
        var service = new SqliteAuthService(db, new Pbkdf2PasswordHasherService(), audit.Object, TestConfiguration);
        await service.BootstrapAdminAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var login = await service.LoginAsync("admin", "SicheresPasswort!123", CancellationToken.None);
        var adminId = (await db.UserAccounts.SingleAsync()).Id;

        var changed = await service.ChangePasswordAsync(
            adminId,
            login.AccessToken!,
            "FalschesPasswort!123",
            "NeuesPasswort!123",
            CancellationToken.None);

        Assert.False(changed.Changed);
        Assert.Equal("InvalidCurrentPassword", changed.ErrorCode);
        Assert.Equal(LoginStatus.Success, (await service.LoginAsync("admin", "SicheresPasswort!123", CancellationToken.None)).Status);
        Assert.Equal(LoginStatus.InvalidCredentials, (await service.LoginAsync("admin", "NeuesPasswort!123", CancellationToken.None)).Status);
        audit.Verify(x => x.LogAsync(
            null,
            "admin",
            "PasswordChanged",
            "Auth",
            It.IsAny<Guid>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static string CreateDatabasePath()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "ShiaiManagerTests", Guid.NewGuid().ToString("N"));
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
