using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// Management endpoints for a tournament's anonymous guest share (enable, disable,
/// rotate, inspect, QR code). Restricted to Admin and Operator roles.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/guest-share")]
[Authorize(Roles = "Admin,Operator")]
public sealed class GuestShareController : ControllerBase
{
    private readonly IGuestShareService _guestShareService;
    private readonly ITournamentStore _tournamentStore;
    private readonly IGuestShareLinkBuilder _linkBuilder;
    private readonly IAuditLogService _auditLog;

    /// <summary>Initializes a new controller instance.</summary>
    public GuestShareController(
        IGuestShareService guestShareService,
        ITournamentStore tournamentStore,
        IGuestShareLinkBuilder linkBuilder,
        IAuditLogService auditLog)
    {
        ArgumentNullException.ThrowIfNull(guestShareService);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        ArgumentNullException.ThrowIfNull(linkBuilder);
        ArgumentNullException.ThrowIfNull(auditLog);
        _guestShareService = guestShareService;
        _tournamentStore = tournamentStore;
        _linkBuilder = linkBuilder;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Returns the current guest-share state and shareable public URL.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GuestShareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestShareResponse>> GetAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var state = await _guestShareService.GetStateAsync(tournamentId, cancellationToken);
        return Ok(ToResponse(state));
    }

    /// <summary>
    /// Creates the share (if absent) and switches it on, optionally with an auto-off expiry.
    /// </summary>
    [HttpPost("enable")]
    [ProducesResponseType(typeof(GuestShareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestShareResponse>> EnableAsync(
        Guid tournamentId,
        [FromBody] GuestShareRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        if (!_linkBuilder.IsHostAllowedForSharing(Request))
        {
            return InsecureHostProblem();
        }

        var state = await _guestShareService.EnableAsync(tournamentId, request?.ExpiresAtUtc, cancellationToken);
        await LogAsync(tournamentId, "GuestShareEnabled", state.ExpiresAtUtc, cancellationToken);
        return Ok(ToResponse(state));
    }

    /// <summary>
    /// Switches the share off without discarding the token.
    /// </summary>
    [HttpPost("disable")]
    [ProducesResponseType(typeof(GuestShareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestShareResponse>> DisableAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var state = await _guestShareService.DisableAsync(tournamentId, cancellationToken);
        await LogAsync(tournamentId, "GuestShareDisabled", state.ExpiresAtUtc, cancellationToken);
        return Ok(ToResponse(state));
    }

    /// <summary>
    /// Generates a fresh token (invalidating the previous QR) and switches the share on.
    /// </summary>
    [HttpPost("rotate")]
    [ProducesResponseType(typeof(GuestShareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestShareResponse>> RotateAsync(
        Guid tournamentId,
        [FromBody] GuestShareRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        if (!_linkBuilder.IsHostAllowedForSharing(Request))
        {
            return InsecureHostProblem();
        }

        var state = await _guestShareService.RotateAsync(tournamentId, request?.ExpiresAtUtc, cancellationToken);
        await LogAsync(tournamentId, "GuestShareRotated", state.ExpiresAtUtc, cancellationToken);
        return Ok(ToResponse(state));
    }

    /// <summary>
    /// Returns the guest URL rendered as an SVG QR code. Requires an existing share.
    /// </summary>
    [HttpGet("qr")]
    [Produces("image/svg+xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQrAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var state = await _guestShareService.GetStateAsync(tournamentId, cancellationToken);
        if (!state.Exists || string.IsNullOrEmpty(state.Token))
        {
            return NotFound();
        }

        if (!_linkBuilder.IsHostAllowedForSharing(Request))
        {
            return InsecureHostProblem();
        }

        var url = _linkBuilder.BuildPublicUrl(Request, tournamentId, state.Token);
        var svg = GenerateQrSvg(url);
        return Content(svg, "image/svg+xml");
    }

    private static string GenerateQrSvg(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(4);
    }

    private ObjectResult InsecureHostProblem()
    {
        return new ObjectResult(new ProblemDetails
        {
            Title = "HTTPS erforderlich.",
            Detail = "Für einen öffentlichen Host ist der Gast-Link nur über eine TLS-gesicherte Verbindung (HTTPS) zulässig.",
            Status = StatusCodes.Status400BadRequest
        })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private GuestShareResponse ToResponse(GuestShareState state)
    {
        var publicUrl = state.Exists
            && !string.IsNullOrEmpty(state.Token)
            && _linkBuilder.IsHostAllowedForSharing(Request)
            ? _linkBuilder.BuildPublicUrl(Request, state.TournamentId, state.Token)
            : null;

        return new GuestShareResponse(
            state.TournamentId,
            state.Exists,
            state.IsEnabled,
            state.IsActive,
            state.Token,
            state.ExpiresAtUtc,
            publicUrl);
    }

    private async Task LogAsync(
        Guid tournamentId,
        string action,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "unbekannt";
        var details = expiresAtUtc.HasValue
            ? $"AutoOff={expiresAtUtc.Value:O}"
            : "AutoOff=none";
        await _auditLog.LogAsync(
            tournamentId, user, action, "Tournament", tournamentId, details, cancellationToken);
    }

    private async Task<bool> TournamentExistsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken) is not null;
    }
}
