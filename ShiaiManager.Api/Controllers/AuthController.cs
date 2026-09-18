using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ShiaiManager.Api.Controllers;

/// <summary>
/// Local authentication endpoints (bootstrap, login, logout, current user).
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("AuthPolicy")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    /// <summary>Initializes a new controller instance.</summary>
    public AuthController(IAuthService authService)
    {
        ArgumentNullException.ThrowIfNull(authService);
        _authService = authService;
    }

    /// <summary>
    /// Creates the first local admin user if no account exists yet.
    /// </summary>
    [HttpPost("bootstrap-admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BootstrapAdminAsync(
        [FromBody] BootstrapAdminRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.BootstrapAdminAsync(request.UserName, request.Password, cancellationToken);
        if (result.Created)
        {
            return StatusCode(StatusCodes.Status201Created);
        }

        if (result.ValidationErrors.Any(e => e.Contains("existiert bereits", StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Initialisierung bereits abgeschlossen.",
                Detail = "Es existiert bereits mindestens ein Benutzer.",
                Status = StatusCodes.Status409Conflict
            });
        }

        foreach (var error in result.ValidationErrors)
        {
            ModelState.AddModelError(nameof(request.Password), error);
        }

        return ValidationProblem(ModelState);
    }

    /// <summary>
    /// Authenticates a local user and returns an access token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<ActionResult<LoginResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request.UserName, request.Password, cancellationToken);
        if (result.Status == LoginStatus.Success)
        {
            Response.Cookies.Append(
                AuthCookie.Name,
                result.AccessToken!,
                AuthCookie.CreateOptions(result.ExpiresAtUtc, Request.IsHttps));

            return Ok(new LoginResponse(
                result.AccessToken!,
                result.ExpiresAtUtc!.Value,
                result.UserName!,
                result.Role!));
        }

        return result.Status switch
        {
            LoginStatus.Locked => StatusCode(StatusCodes.Status423Locked, new ProblemDetails
            {
                Title = "Benutzer ist gesperrt.",
                Detail = "Zu viele fehlgeschlagene Anmeldeversuche. Bitte später erneut versuchen.",
                Status = StatusCodes.Status423Locked
            }),
            LoginStatus.Inactive => Unauthorized(new ProblemDetails
            {
                Title = "Benutzer ist deaktiviert.",
                Detail = "Der Benutzer ist nicht aktiv.",
                Status = StatusCodes.Status401Unauthorized
            }),
            _ => Unauthorized(new ProblemDetails
            {
                Title = "Anmeldung fehlgeschlagen.",
                Detail = "Benutzername oder Passwort ist ungültig.",
                Status = StatusCodes.Status401Unauthorized
            })
        };
    }

    /// <summary>
    /// Revokes the current bearer token.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var token = ReadAuthToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            await _authService.LogoutAsync(token, cancellationToken);
        }

        Response.Cookies.Delete(AuthCookie.Name, AuthCookie.CreateOptions(isHttps: Request.IsHttps));

        return NoContent();
    }

    /// <summary>
    /// Returns basic identity information of the current authenticated user.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthenticatedUser), StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var nameClaim = User.Identity?.Name ?? "unbekannt";
        var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Unknown";

        var userId = Guid.TryParse(idClaim, out var parsed) ? parsed : Guid.Empty;
        return Ok(new AuthenticatedUser(userId, nameClaim, roleClaim));
    }

    /// <summary>
    /// Returns all local user accounts. Admin only.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<LocalUserAccount>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LocalUserAccount>>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return Ok(await _authService.GetUsersAsync(cancellationToken));
    }

    /// <summary>
    /// Creates a local user account. Admin only.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("users")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name ?? "unbekannt";
        var result = await _authService.CreateUserAsync(actor, request.UserName, request.Role, request.Password, cancellationToken);
        if (result.Created)
        {
            return Created($"/api/auth/users/{result.UserId}", null);
        }

        foreach (var error in result.ValidationErrors)
        {
            ModelState.AddModelError(nameof(request.Password), error);
        }

        return ValidationProblem(ModelState);
    }

    /// <summary>
    /// Activates or deactivates a local user account. Admin only.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPatch("users/{userId:guid}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetUserActiveStateAsync(
        Guid userId,
        [FromBody] SetUserActiveRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name ?? "unbekannt";
        var result = await _authService.SetUserActiveStateAsync(actor, userId, request.IsActive, cancellationToken);
        if (result.Updated)
        {
            return NoContent();
        }

        if (string.Equals(result.ErrorCode, "NotFound", StringComparison.Ordinal))
        {
            return NotFound();
        }

        return Conflict(new ProblemDetails
        {
            Title = "Benutzerstatus konnte nicht aktualisiert werden.",
            Detail = result.ErrorMessage,
            Status = StatusCodes.Status409Conflict
        });
    }

    /// <summary>
    /// Deletes a local user account. Admin only.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("users/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name ?? "unbekannt";
        var result = await _authService.DeleteUserAsync(actor, userId, cancellationToken);
        if (result.Deleted)
        {
            return NoContent();
        }

        if (string.Equals(result.ErrorCode, "NotFound", StringComparison.Ordinal))
        {
            return NotFound();
        }

        return Conflict(new ProblemDetails
        {
            Title = "Benutzer konnte nicht gelöscht werden.",
            Detail = result.ErrorMessage,
            Status = StatusCodes.Status409Conflict
        });
    }

    /// <summary>
    /// Resets a local user password. Admin only.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("users/{userId:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPasswordAsync(
        Guid userId,
        [FromBody] ResetUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var actor = User.Identity?.Name ?? "unbekannt";
        var result = await _authService.ResetPasswordAsync(actor, userId, request.NewPassword, cancellationToken);
        if (result.Updated)
        {
            Response.Cookies.Delete(AuthCookie.Name, AuthCookie.CreateOptions(isHttps: Request.IsHttps));
            return NoContent();
        }

        if (string.Equals(result.ErrorCode, "NotFound", StringComparison.Ordinal))
        {
            return NotFound();
        }

        if (result.ValidationErrors is { Count: > 0 })
        {
            foreach (var error in result.ValidationErrors)
            {
                ModelState.AddModelError(nameof(request.NewPassword), error);
            }

            return ValidationProblem(ModelState);
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Passwort konnte nicht zurückgesetzt werden.",
            Detail = result.ErrorMessage,
            Status = StatusCodes.Status400BadRequest
        });
    }

    /// <summary>
    /// Changes the password of the currently authenticated local user.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var token = ReadAuthToken();
        if (!Guid.TryParse(idClaim, out var userId) || string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized();
        }

        var result = await _authService.ChangePasswordAsync(
            userId,
            token,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
        if (result.Changed)
        {
            return NoContent();
        }

        if (string.Equals(result.ErrorCode, "InvalidSession", StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        if (string.Equals(result.ErrorCode, "InvalidCurrentPassword", StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(request.CurrentPassword), result.ErrorMessage ?? "Aktuelles Passwort ist ungültig.");
            return ValidationProblem(ModelState);
        }

        if (result.ValidationErrors is { Count: > 0 })
        {
            foreach (var error in result.ValidationErrors)
            {
                ModelState.AddModelError(nameof(request.NewPassword), error);
            }

            return ValidationProblem(ModelState);
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Passwort konnte nicht geändert werden.",
            Detail = result.ErrorMessage,
            Status = StatusCodes.Status400BadRequest
        });
    }

    private string? ReadAuthToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var bearerToken = header["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                return bearerToken;
            }
        }

        return Request.Cookies.TryGetValue(AuthCookie.Name, out var cookieToken)
            && !string.IsNullOrWhiteSpace(cookieToken)
            ? cookieToken
            : null;
    }
}
