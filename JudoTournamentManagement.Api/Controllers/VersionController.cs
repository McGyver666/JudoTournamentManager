using System.Reflection;
using JudoTournamentManagement.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// Lightweight endpoint exposing the deployed application version so operators
/// can confirm which release is running. Anonymous so it works before login.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/version")]
public sealed class VersionController : ControllerBase
{
    private static readonly string ResolvedVersion = ResolveVersion();

    /// <summary>
    /// Returns the deployed application version.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(VersionResponse), StatusCodes.Status200OK)]
    public ActionResult<VersionResponse> GetVersion() =>
        Ok(new VersionResponse(ResolvedVersion));

    private static string ResolveVersion()
    {
        var informational = typeof(VersionController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational) ? "0.0.0-dev" : informational;
    }
}
