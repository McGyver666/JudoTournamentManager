using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Manages the anonymous, time-boxed guest share for a tournament's match lists.
/// Exactly one share token exists per tournament.
/// </summary>
public interface IGuestShareService
{
    /// <summary>Role name assigned to an anonymous guest principal.</summary>
    public const string GuestRole = "Guest";

    /// <summary>Claim type carrying the tournament a guest token is scoped to.</summary>
    public const string TournamentClaimType = "tournamentId";

    /// <summary>
    /// Returns the current share state for a tournament (never null; reports
    /// <see cref="GuestShareState.Exists"/> = false when no share was created).
    /// </summary>
    Task<GuestShareState> GetStateAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the share (if absent) and switches it on, optionally with an
    /// auto-off expiry. Reuses the existing token when one already exists.
    /// </summary>
    Task<GuestShareState> EnableAsync(
        Guid tournamentId,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Switches the share off without discarding the token.</summary>
    Task<GuestShareState> DisableAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Generates a fresh token (invalidating the previous QR), switches the
    /// share on and applies the optional auto-off expiry.
    /// </summary>
    Task<GuestShareState> RotateAsync(
        Guid tournamentId,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a plaintext token to the tournament it grants access to, or
    /// null when the token is unknown, disabled or expired.
    /// </summary>
    Task<Guid?> ValidateTokenAsync(string token, CancellationToken cancellationToken);
}
