using System.Security.Cryptography;
using System.Text;
using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed authentication service for local offline operation.
/// </summary>
public sealed class SqliteAuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(8);
    private static readonly string[] AllowedRoles = ["Admin", "Operator", "Display"];

    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IAuditLogService _auditLogService;
    private readonly byte[] _tokenHmacKey;

    /// <summary>Initializes a new service instance.</summary>
    public SqliteAuthService(
        AppDbContext dbContext,
        IPasswordHasherService passwordHasher,
        IAuditLogService auditLogService,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(auditLogService);
        ArgumentNullException.ThrowIfNull(configuration);
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
        _tokenHmacKey = ResolveTokenHmacKey(configuration);
    }

    /// <inheritdoc />
    public async Task<BootstrapAdminResult> BootstrapAdminAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.UserAccounts.AnyAsync(cancellationToken))
        {
            return new BootstrapAdminResult(false, ["Mindestens ein Benutzer existiert bereits."]);
        }

        var validationErrors = ValidateCredentials(userName, password);
        if (validationErrors.Count > 0)
        {
            return new BootstrapAdminResult(false, validationErrors);
        }

        var normalized = NormalizeUserName(userName);
        var hashResult = _passwordHasher.HashPassword(password);
        var now = DateTimeOffset.UtcNow;

        var user = new UserAccountRecord
        {
            Id = Guid.NewGuid(),
            UserName = userName.Trim(),
            NormalizedUserName = normalized,
            Role = "Admin",
            PasswordHash = hashResult.Hash,
            PasswordSalt = hashResult.Salt,
            PasswordIterations = hashResult.Iterations,
            IsActive = true,
            FailedLoginCount = 0,
            LockedUntilUtc = null,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        _dbContext.UserAccounts.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            null,
            user.UserName,
            "AdminBootstrapped",
            "Auth",
            user.Id,
            "Erster lokaler Admin wurde erstellt.",
            cancellationToken);

        return new BootstrapAdminResult(true, []);
    }

    /// <inheritdoc />
    public async Task<LoginResult> LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var normalized = NormalizeUserName(userName);
        var account = await _dbContext.UserAccounts
            .SingleOrDefaultAsync(x => x.NormalizedUserName == normalized, cancellationToken);

        if (account is null)
        {
            await LogLoginFailedAsync(userName, "Unbekannter Benutzer.", cancellationToken);
            return new LoginResult(LoginStatus.InvalidCredentials, null, null, null, null);
        }

        if (!account.IsActive)
        {
            await LogLoginFailedAsync(account.UserName, "Benutzer ist deaktiviert.", cancellationToken);
            return new LoginResult(LoginStatus.Inactive, null, null, null, null);
        }

        var now = DateTimeOffset.UtcNow;
        if (account.LockedUntilUtc.HasValue && account.LockedUntilUtc.Value > now)
        {
            await LogLoginFailedAsync(account.UserName, "Benutzer ist gesperrt.", cancellationToken);
            return new LoginResult(LoginStatus.Locked, null, null, null, null);
        }

        var isValid = _passwordHasher.Verify(
            password,
            account.PasswordHash,
            account.PasswordSalt,
            account.PasswordIterations);

        if (!isValid)
        {
            account.FailedLoginCount++;
            account.UpdatedUtc = now;

            if (account.FailedLoginCount >= MaxFailedAttempts)
            {
                account.LockedUntilUtc = now.Add(LockDuration);
                account.FailedLoginCount = 0;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await LogLoginFailedAsync(account.UserName, "Ungültige Zugangsdaten.", cancellationToken);
            return new LoginResult(LoginStatus.InvalidCredentials, null, null, null, null);
        }

        account.FailedLoginCount = 0;
        account.LockedUntilUtc = null;
        account.UpdatedUtc = now;

        var token = GenerateToken();
        var tokenHash = HashToken(token);
        var expires = now.Add(SessionDuration);

        var session = new AuthSessionRecord
        {
            Id = Guid.NewGuid(),
            UserAccountId = account.Id,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = expires,
            RevokedAtUtc = null
        };

        _dbContext.AuthSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            null,
            account.UserName,
            "LoginSucceeded",
            "Auth",
            account.Id,
            null,
            cancellationToken);

        return new LoginResult(LoginStatus.Success, token, expires, account.UserName, account.Role);
    }

    /// <inheritdoc />
    public async Task<bool> LogoutAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var tokenHash = HashToken(token);
        var candidates = await _dbContext.AuthSessions
            .Include(x => x.UserAccount)
            .Where(x => x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        var session = candidates.SingleOrDefault(x => x.TokenHash.SequenceEqual(tokenHash));

        if (session is null)
        {
            return false;
        }

        session.RevokedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            null,
            session.UserAccount?.UserName ?? "unbekannt",
            "LogoutSucceeded",
            "Auth",
            session.UserAccountId,
            null,
            cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<AuthenticatedUser?> ValidateTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = HashToken(token);
        var now = DateTimeOffset.UtcNow;

        var sessions = await _dbContext.AuthSessions
            .AsNoTracking()
            .Include(x => x.UserAccount)
            .Where(x => x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        var session = sessions.SingleOrDefault(
            x => x.ExpiresAtUtc > now && x.TokenHash.SequenceEqual(tokenHash));

        var account = session?.UserAccount;
        if (account is null || !account.IsActive)
        {
            return null;
        }

        return new AuthenticatedUser(account.Id, account.UserName, account.Role);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalUserAccount>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.UserAccounts
            .AsNoTracking()
            .OrderBy(x => x.UserName)
            .Select(x => new LocalUserAccount(
                x.Id,
                x.UserName,
                x.Role,
                x.IsActive,
                x.CreatedUtc,
                x.UpdatedUtc))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CreateUserResult> CreateUserAsync(
        string actorUserName,
        string userName,
        string role,
        string password,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateCredentials(userName, password);
        var normalizedRole = NormalizeRole(role);
        if (normalizedRole is null)
        {
            validationErrors.Add("Ungültige Rolle. Erlaubt sind: Admin, Operator, Display.");
        }

        var normalizedUser = NormalizeUserName(userName);
        if (await _dbContext.UserAccounts.AnyAsync(x => x.NormalizedUserName == normalizedUser, cancellationToken))
        {
            validationErrors.Add("Benutzername existiert bereits.");
        }

        if (validationErrors.Count > 0)
        {
            return new CreateUserResult(false, null, validationErrors);
        }

        var hashResult = _passwordHasher.HashPassword(password);
        var now = DateTimeOffset.UtcNow;

        var user = new UserAccountRecord
        {
            Id = Guid.NewGuid(),
            UserName = userName.Trim(),
            NormalizedUserName = normalizedUser,
            Role = normalizedRole!,
            PasswordHash = hashResult.Hash,
            PasswordSalt = hashResult.Salt,
            PasswordIterations = hashResult.Iterations,
            IsActive = true,
            FailedLoginCount = 0,
            LockedUntilUtc = null,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        _dbContext.UserAccounts.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            null,
            string.IsNullOrWhiteSpace(actorUserName) ? "unbekannt" : actorUserName.Trim(),
            "UserCreated",
            "Auth",
            user.Id,
            $"User={user.UserName}; Role={user.Role}",
            cancellationToken);

        return new CreateUserResult(true, user.Id, []);
    }

    /// <inheritdoc />
    public async Task<UpdateUserStateResult> SetUserActiveStateAsync(
        string actorUserName,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.UserAccounts.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UpdateUserStateResult(false, "NotFound", "Benutzer wurde nicht gefunden.");
        }

        if (string.Equals(actorUserName?.Trim(), user.UserName, StringComparison.OrdinalIgnoreCase) && !isActive)
        {
            return new UpdateUserStateResult(false, "SelfDeactivate", "Eigener Benutzer kann nicht deaktiviert werden.");
        }

        user.IsActive = isActive;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.UpdatedUtc = DateTimeOffset.UtcNow;

        if (!isActive)
        {
            var sessions = await _dbContext.AuthSessions
                .Where(x => x.UserAccountId == userId && x.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var session in sessions)
            {
                session.RevokedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            null,
            string.IsNullOrWhiteSpace(actorUserName) ? "unbekannt" : actorUserName.Trim(),
            isActive ? "UserActivated" : "UserDeactivated",
            "Auth",
            userId,
            $"User={user.UserName}",
            cancellationToken);

        return new UpdateUserStateResult(true, null, null);
    }

    /// <inheritdoc />
    public async Task<ResetPasswordResult> ResetPasswordAsync(
        string actorUserName,
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.UserAccounts.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return new ResetPasswordResult(false, "NotFound", "Benutzer wurde nicht gefunden.", null);
        }

        var errors = ValidateCredentials(user.UserName, newPassword);
        if (errors.Count > 0)
        {
            return new ResetPasswordResult(false, null, null, errors);
        }

        var hashResult = _passwordHasher.HashPassword(newPassword);
        user.PasswordHash = hashResult.Hash;
        user.PasswordSalt = hashResult.Salt;
        user.PasswordIterations = hashResult.Iterations;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.UpdatedUtc = DateTimeOffset.UtcNow;

        var sessions = await _dbContext.AuthSessions
            .Where(x => x.UserAccountId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAtUtc = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            null,
            string.IsNullOrWhiteSpace(actorUserName) ? "unbekannt" : actorUserName.Trim(),
            "PasswordReset",
            "Auth",
            userId,
            $"User={user.UserName}",
            cancellationToken);

        return new ResetPasswordResult(true, null, null, null);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private byte[] HashToken(string token)
    {
        return HMACSHA256.HashData(_tokenHmacKey, Encoding.UTF8.GetBytes(token));
    }

    private static byte[] ResolveTokenHmacKey(IConfiguration configuration)
    {
        var secret = configuration["Security:AuthTokenHmacSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "Missing required configuration key 'Security:AuthTokenHmacSecret'.");
        }

        var key = Encoding.UTF8.GetBytes(secret);
        if (key.Length < 32)
        {
            throw new InvalidOperationException(
                "Configuration key 'Security:AuthTokenHmacSecret' must be at least 32 characters long.");
        }

        return key;
    }

    private static string NormalizeUserName(string userName)
    {
        return userName.Trim().ToUpperInvariant();
    }

    private static string? NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        var normalized = role.Trim();
        return AllowedRoles.SingleOrDefault(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ValidateCredentials(string userName, string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add("Benutzername ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Passwort ist erforderlich.");
            return errors;
        }

        if (password.Length < 12)
        {
            errors.Add("Passwort muss mindestens 12 Zeichen lang sein.");
        }

        var classes = 0;
        if (password.Any(char.IsUpper)) classes++;
        if (password.Any(char.IsLower)) classes++;
        if (password.Any(char.IsDigit)) classes++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) classes++;

        if (classes < 3)
        {
            errors.Add("Passwort muss mindestens 3 Zeichentypen enthalten: Großbuchstaben, Kleinbuchstaben, Zahlen, Sonderzeichen.");
        }

        return errors;
    }

    private Task LogLoginFailedAsync(string userName, string details, CancellationToken cancellationToken)
    {
        return _auditLogService.LogAsync(
            null,
            string.IsNullOrWhiteSpace(userName) ? "unbekannt" : userName.Trim(),
            "LoginFailed",
            "Auth",
            null,
            details,
            cancellationToken);
    }
}
