using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoint exposing the Current / Next / On-deck fight queue for a tatami (F-01).
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/tatamis/{tatamiId:guid}/queue")]
public sealed class TatamiQueueController : ControllerBase
{
    private readonly ITatamiQueueService _queueService;

    /// <summary>Initializes a new controller instance.</summary>
    public TatamiQueueController(ITatamiQueueService queueService)
    {
        ArgumentNullException.ThrowIfNull(queueService);
        _queueService = queueService;
    }

    /// <summary>
    /// Returns the fight queue for a tatami.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(TatamiQueue), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TatamiQueue>> GetQueueAsync(
        Guid tournamentId,
        Guid tatamiId,
        CancellationToken cancellationToken)
    {
        var queue = await _queueService.GetQueueAsync(tournamentId, tatamiId, cancellationToken);
        return queue is null ? NotFound() : Ok(queue);
    }
}
