using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Local authentication service for bootstrap, login, logout and token validation.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Creates the first admin account if no users exist.
    /// </summary>
    Task<BootstrapAdminResult> BootstrapAdminAsync(string userName, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Authenticates a user and issues a bearer token.
    /// </summary>
    Task<LoginResult> LoginAsync(string userName, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the current bearer token.
    /// </summary>
    Task<bool> LogoutAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a valid token to an authenticated user identity.
    /// </summary>
    Task<AuthenticatedUser?> ValidateTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Returns all local user accounts (without sensitive password fields).
    /// </summary>
    Task<IReadOnlyList<LocalUserAccount>> GetUsersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a local user account.
    /// </summary>
    Task<CreateUserResult> CreateUserAsync(string actorUserName, string userName, string role, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Activates or deactivates a local user account.
    /// </summary>
    Task<UpdateUserStateResult> SetUserActiveStateAsync(string actorUserName, Guid userId, bool isActive, CancellationToken cancellationToken);

    /// <summary>
    /// Resets a local user's password.
    /// </summary>
    Task<ResetPasswordResult> ResetPasswordAsync(string actorUserName, Guid userId, string newPassword, CancellationToken cancellationToken);
}

/// <summary>
/// Result of bootstrapping the first local admin account.
/// </summary>
public sealed record BootstrapAdminResult(bool Created, IReadOnlyList<string> ValidationErrors);

/// <summary>
/// Result of a login attempt.
/// </summary>
public sealed record LoginResult(
    LoginStatus Status,
    string? AccessToken,
    DateTimeOffset? ExpiresAtUtc,
    string? UserName,
    string? Role);

/// <summary>
/// Safe user account projection used for admin management endpoints.
/// </summary>
public sealed record LocalUserAccount(
    Guid Id,
    string UserName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

/// <summary>
/// Result of creating a local user.
/// </summary>
public sealed record CreateUserResult(bool Created, Guid? UserId, IReadOnlyList<string> ValidationErrors);

/// <summary>
/// Result of activating/deactivating a user.
/// </summary>
public sealed record UpdateUserStateResult(bool Updated, string? ErrorCode, string? ErrorMessage);

/// <summary>
/// Result of resetting a user password.
/// </summary>
public sealed record ResetPasswordResult(bool Updated, string? ErrorCode, string? ErrorMessage, IReadOnlyList<string>? ValidationErrors);
