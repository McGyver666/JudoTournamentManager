namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistent login session issued by the authentication service.
/// Only a token hash is stored; plaintext tokens never persist.
/// </summary>
public sealed class AuthSessionRecord
{
    /// <summary>Primary identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning user account identifier.</summary>
    public Guid UserAccountId { get; set; }

    /// <summary>Navigation to user account.</summary>
    public UserAccountRecord? UserAccount { get; set; }

    /// <summary>SHA-256 hash of the bearer token.</summary>
    public byte[] TokenHash { get; set; } = [];

    /// <summary>UTC session creation timestamp.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>UTC expiration timestamp.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>UTC revocation timestamp when the session was logged out.</summary>
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
