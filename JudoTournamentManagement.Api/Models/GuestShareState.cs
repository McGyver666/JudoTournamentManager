namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Current state of a tournament's anonymous guest share.
/// </summary>
/// <param name="TournamentId">Owning tournament identifier.</param>
/// <param name="Exists">Whether a share token has ever been created.</param>
/// <param name="IsEnabled">Whether the share is switched on.</param>
/// <param name="IsActive">Whether the share currently grants access (enabled and not expired).</param>
/// <param name="Token">The plaintext share token; null when no share exists.</param>
/// <param name="ExpiresAtUtc">Optional auto-off timestamp; null means no automatic expiry.</param>
public sealed record GuestShareState(
    Guid TournamentId,
    bool Exists,
    bool IsEnabled,
    bool IsActive,
    string? Token,
    DateTimeOffset? ExpiresAtUtc);
