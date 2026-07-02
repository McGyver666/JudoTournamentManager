namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistent local user account for offline authentication.
/// </summary>
public sealed class UserAccountRecord
{
    /// <summary>Primary identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Display/login name as entered by the user.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Case-insensitive lookup key.</summary>
    public string NormalizedUserName { get; set; } = string.Empty;

    /// <summary>User role (e.g. Admin, Operator, Display).</summary>
    public string Role { get; set; } = "Admin";

    /// <summary>PBKDF2 hash bytes of the password.</summary>
    public byte[] PasswordHash { get; set; } = [];

    /// <summary>Per-user random salt for PBKDF2.</summary>
    public byte[] PasswordSalt { get; set; } = [];

    /// <summary>Iteration count used for PBKDF2 derivation.</summary>
    public int PasswordIterations { get; set; }

    /// <summary>Whether the account can authenticate.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Consecutive failed login attempts.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Optional lock until this UTC timestamp after too many failures.</summary>
    public DateTimeOffset? LockedUntilUtc { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
