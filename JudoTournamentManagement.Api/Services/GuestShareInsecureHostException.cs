namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Thrown when a guest-share link or QR code is requested for a non-local,
/// public host that is not served over TLS. Delivering the share token over a
/// cleartext connection to a public host would leak it, so this is refused.
/// </summary>
public sealed class GuestShareInsecureHostException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public GuestShareInsecureHostException()
        : base("Der Gast-Link ist für einen öffentlichen Host nur über HTTPS zulässig.")
    {
    }
}
