using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShiaiManager.Api.Controllers;

/// <summary>
/// API endpoints for the live tournament control-stand overview.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}")]
public sealed class TournamentOverviewController : ControllerBase
{
    private readonly IOverviewStatsService _overviewStatsService;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>Initializes a new controller instance.</summary>
    public TournamentOverviewController(
        IOverviewStatsService overviewStatsService,
        ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(overviewStatsService);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _overviewStatsService = overviewStatsService;
        _tournamentStore = tournamentStore;
    }

    /// <summary>Returns aggregate statistics for the selected tournament.</summary>
    [Authorize]
    [HttpGet("overview-stats")]
    [ProducesResponseType(typeof(TournamentOverviewStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentOverviewStats>> GetStatsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        return Ok(await _overviewStatsService.GetAsync(tournamentId, cancellationToken));
    }
}