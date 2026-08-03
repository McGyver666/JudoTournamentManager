using Microsoft.AspNetCore.Http;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Builds the public, shareable guest URL that a QR code encodes.
/// </summary>
public interface IGuestShareLinkBuilder
{
    /// <summary>
    /// Builds the absolute public match-list URL for a guest token.
    /// Uses the configured base URL override when present, otherwise the
    /// scheme and host of the current request.
    /// </summary>
    /// <exception cref="GuestShareInsecureHostException">
    /// Thrown when the effective host is public (non-local) and not served over TLS.
    /// </exception>
    string BuildPublicUrl(HttpRequest request, Guid tournamentId, string token);

    /// <summary>
    /// Returns <c>true</c> when the effective host may safely deliver a guest
    /// link. A public (non-local) host is only allowed over TLS; local/LAN hosts
    /// are accepted over plain HTTP.
    /// </summary>
    bool IsHostAllowedForSharing(HttpRequest request);
}
