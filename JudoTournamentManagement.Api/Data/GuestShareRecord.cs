namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistent anonymous guest-share token for a tournament. Exactly one row per
/// tournament grants time-boxed, read-only public access to that tournament's
/// match lists. The token is stored in plaintext so the QR code can be
/// re-displayed on demand; it is a low-sensitivity, revocable display token.
/// </summary>
public sealed class GuestShareRecord
{
    /// <summary>Primary identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tournament identifier (unique: one share per tournament).</summary>
    public Guid TournamentId { get; set; }

    /// <summary>Navigation to the owning tournament.</summary>
    public TournamentRecord? Tournament { get; set; }

    /// <summary>Opaque, high-entropy share token (base64url, plaintext).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Whether the share is currently switched on.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Optional auto-off safety net; null means no automatic expiry.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC timestamp of the last state change (enable/disable/rotate).</summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>UTC timestamp of the last token rotation, if any.</summary>
    public DateTimeOffset? RotatedAtUtc { get; set; }
}
