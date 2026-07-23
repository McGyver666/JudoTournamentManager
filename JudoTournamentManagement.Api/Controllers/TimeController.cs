using JudoTournamentManagement.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// Lightweight endpoint exposing current server UTC time for client-side clock synchronization.
/// </summary>
[ApiController]
[Authorize]
[Route("api/time")]
public sealed class TimeController : ControllerBase
{
    /// <summary>
    /// Returns current server time in UTC.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ServerTimeResponse), StatusCodes.Status200OK)]
    public ActionResult<ServerTimeResponse> GetServerTime() =>
        Ok(new ServerTimeResponse(DateTimeOffset.UtcNow));
}
