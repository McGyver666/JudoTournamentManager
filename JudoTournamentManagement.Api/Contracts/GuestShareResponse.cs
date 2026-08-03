namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Response describing the current guest-share state plus the shareable public URL.
/// </summary>
/// <param name="TournamentId">Owning tournament identifier.</param>
/// <param name="Exists">Whether a share token has ever been created.</param>
/// <param name="IsEnabled">Whether the share is switched on.</param>
/// <param name="IsActive">Whether the share currently grants access (enabled and not expired).</param>
/// <param name="Token">The plaintext share token; null when no share exists.</param>
/// <param name="ExpiresAtUtc">Optional auto-off timestamp; null means no automatic expiry.</param>
/// <param name="PublicUrl">The shareable guest URL (token embedded); null when no share exists.</param>
public sealed record GuestShareResponse(
    Guid TournamentId,
    bool Exists,
    bool IsEnabled,
    bool IsActive,
    string? Token,
    DateTimeOffset? ExpiresAtUtc,
    string? PublicUrl);
