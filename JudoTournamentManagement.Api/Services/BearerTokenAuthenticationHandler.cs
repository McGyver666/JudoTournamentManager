using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Authentication handler for local bearer tokens issued by <see cref="IAuthService"/>.
/// </summary>
public sealed class BearerTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAuthService _authService;
    private readonly IGuestShareService _guestShareService;

    /// <summary>Initializes a new handler instance.</summary>
    public BearerTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAuthService authService,
        IGuestShareService guestShareService)
        : base(options, logger, encoder)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(guestShareService);
        _authService = authService;
        _guestShareService = guestShareService;
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = TryReadBearerTokenFromHeader();
        if (string.IsNullOrWhiteSpace(token) && Request.Path.StartsWithSegments("/hubs/tournament"))
        {
            token = Request.Query["access_token"].ToString();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        var user = await _authService.ValidateTokenAsync(token, Context.RequestAborted);
        if (user is not null)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role)
            };

            return Success(claims);
        }

        var guestTournamentId = await _guestShareService.ValidateTokenAsync(token, Context.RequestAborted);
        if (guestTournamentId is not null)
        {
            var guestClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, $"guest:{guestTournamentId}"),
                new Claim(ClaimTypes.Name, "guest"),
                new Claim(ClaimTypes.Role, IGuestShareService.GuestRole),
                new Claim(IGuestShareService.TournamentClaimType, guestTournamentId.Value.ToString())
            };

            return Success(guestClaims);
        }

        return AuthenticateResult.Fail("Token ist ungültig oder abgelaufen.");
    }

    private AuthenticateResult Success(Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private string? TryReadBearerTokenFromHeader()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return header["Bearer ".Length..].Trim();
    }
}
