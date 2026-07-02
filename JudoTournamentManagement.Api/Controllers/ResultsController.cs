using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for tournament results: medal table (G-03).
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}")]
public sealed class ResultsController : ControllerBase
{
    private readonly IRankingService _rankingService;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>Initializes a new controller instance.</summary>
    public ResultsController(IRankingService rankingService, ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(rankingService);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _rankingService = rankingService;
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Returns the medal table for a tournament, sorted by gold, silver, bronze, then club name.
    /// </summary>
    [Authorize]
    [HttpGet("medal-table")]
    [ProducesResponseType(typeof(IReadOnlyList<MedalEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MedalEntry>>> GetMedalTableAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        var table = await _rankingService.GetMedalTableAsync(tournamentId, cancellationToken);
        return Ok(table);
    }
}
