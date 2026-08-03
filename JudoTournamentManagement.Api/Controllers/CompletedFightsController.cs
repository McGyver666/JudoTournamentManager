using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoint for the tournament-wide combat overview (Kampfübersicht): all completed fights.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}")]
public sealed class CompletedFightsController : ControllerBase
{
    private readonly ICompletedFightsService _completedFightsService;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>Initializes a new controller instance.</summary>
    public CompletedFightsController(
        ICompletedFightsService completedFightsService,
        ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(completedFightsService);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _completedFightsService = completedFightsService;
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Returns all completed (non-bye) fights of a tournament, most recently completed first,
    /// with resolved athlete, club, category, and tatami names.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpGet("completed-fights")]
    [ProducesResponseType(typeof(IReadOnlyList<CompletedFightSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CompletedFightSummary>>> GetCompletedFightsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        var fights = await _completedFightsService.GetCompletedFightsAsync(tournamentId, cancellationToken);
        return Ok(fights);
    }
}
