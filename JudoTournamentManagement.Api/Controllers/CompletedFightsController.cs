using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
    private readonly IMatchService _matchService;

    /// <summary>Initializes a new controller instance.</summary>
    public CompletedFightsController(
        ICompletedFightsService completedFightsService,
        ITournamentStore tournamentStore,
        IMatchService matchService)
    {
        ArgumentNullException.ThrowIfNull(completedFightsService);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        ArgumentNullException.ThrowIfNull(matchService);
        _completedFightsService = completedFightsService;
        _tournamentStore = tournamentStore;
        _matchService = matchService;
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
        if (tournament is null) return NotFound();

        var fights = await _completedFightsService.GetCompletedFightsAsync(tournamentId, cancellationToken);
        return Ok(fights);
    }

    /// <summary>
    /// Edits the scores and winner of a completed, non-group-stage fight. Requires Admin role.
    /// When downstream fights already started would be affected and Confirmed is false,
    /// returns 409 with the list of affected fights (ConfirmationRequired).
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("completed-fights/{fightId:guid}/edit-result")]
    [ProducesResponseType(typeof(EditFightResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EditResultAsync(
        Guid tournamentId,
        Guid fightId,
        [FromBody] EditFightResultRequest request,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null) return NotFound();

        var user = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var response = await _matchService.EditResultAsync(fightId, request, user, cancellationToken);

        return response.Status switch
        {
            EditResultStatus.Success              => NoContent(),
            EditResultStatus.ConfirmationRequired => Ok(response),   // 200 so Angular next() handles it
            EditResultStatus.FightNotFound        => NotFound(),
            EditResultStatus.InvalidState         => UnprocessableEntity(response),
            EditResultStatus.WinnerNotParticipant => UnprocessableEntity(response),
            _                                     => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
