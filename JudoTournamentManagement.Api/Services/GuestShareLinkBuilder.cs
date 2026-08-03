using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Default <see cref="IGuestShareLinkBuilder"/>. Resolves the base URL from the
/// optional <c>GuestShare:PublicBaseUrl</c> configuration value and falls back
/// to the current request's scheme and host.
/// </summary>
public sealed class GuestShareLinkBuilder : IGuestShareLinkBuilder
{
    /// <summary>Relative path of the public match-list view.</summary>
    private const string PublicPath = "/public/match-lists";

    private readonly string? _configuredBaseUrl;

    /// <summary>Initializes a new builder instance.</summary>
    public GuestShareLinkBuilder(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = configuration["GuestShare:PublicBaseUrl"];
        _configuredBaseUrl = string.IsNullOrWhiteSpace(configured) ? null : configured.TrimEnd('/');
    }

    /// <inheritdoc />
    public string BuildPublicUrl(HttpRequest request, Guid tournamentId, string token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var (scheme, host, baseUrl) = ResolveBase(request);
        if (!IsAllowed(scheme, host))
        {
            throw new GuestShareInsecureHostException();
        }

        var encodedToken = Uri.EscapeDataString(token);
        return $"{baseUrl}{PublicPath}?t={encodedToken}&tid={tournamentId}";
    }

    /// <inheritdoc />
    public bool IsHostAllowedForSharing(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (scheme, host, _) = ResolveBase(request);
        return IsAllowed(scheme, host);
    }

    private (string Scheme, string Host, string BaseUrl) ResolveBase(HttpRequest request)
    {
        if (_configuredBaseUrl is not null
            && Uri.TryCreate(_configuredBaseUrl, UriKind.Absolute, out var configuredUri))
        {
            return (configuredUri.Scheme, configuredUri.Host, _configuredBaseUrl);
        }

        return (request.Scheme, request.Host.Host, $"{request.Scheme}://{request.Host.Value}");
    }

    private static bool IsAllowed(string scheme, string host)
    {
        // HTTPS is always acceptable. Plain HTTP is only acceptable on a
        // local/LAN host, never on a public host (token would travel cleartext).
        return string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || IsLocalHost(host);
    }

    private static bool IsLocalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".home.arpa", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            return IsPrivateOrLoopback(ip);
        }

        // A single-label host name (no dot) is a LAN name (e.g. "kampfrichter-pc").
        return !host.Contains('.');
    }

    private static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                127 => true,
                172 => bytes[1] >= 16 && bytes[1] <= 31,
                192 => bytes[1] == 168,
                169 => bytes[1] == 254, // link-local
                _ => false
            };
        }

        // IPv6 unique-local (fc00::/7) and link-local (fe80::/10).
        return ip.IsIPv6LinkLocal
            || ip.IsIPv6SiteLocal
            || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
    }
}
