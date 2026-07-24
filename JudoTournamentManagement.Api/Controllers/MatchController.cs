using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for operating a single fight on a tatami: tatami assignment, start, scoring,
/// winner confirmation and result correction (F-02, F-03).
/// </summary>
[ApiController]
[Authorize(Roles = "Admin,Operator")]
[Route("api/tournaments/{tournamentId:guid}/fights/{fightId:guid}")]
public sealed class MatchController : ControllerBase
{
    private readonly IMatchService _matchService;
    private readonly IFightsStore _fightsStore;

    /// <summary>Initializes a new controller instance.</summary>
    public MatchController(IMatchService matchService, IFightsStore fightsStore)
    {
        ArgumentNullException.ThrowIfNull(matchService);
        ArgumentNullException.ThrowIfNull(fightsStore);
        _matchService = matchService;
        _fightsStore = fightsStore;
    }

    /// <summary>
    /// Assigns the fight to a tatami (or clears it). Requires the Admin role.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("assign-tatami")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignTatamiAsync(
        Guid tournamentId,
        Guid fightId,
        [FromBody] AssignTatamiRequest request,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.AssignTatamiAsync(fightId, request.TatamiId, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Assigns many fights to tatamis in a single atomic request. Requires the Admin role.
    /// Fights whose athletes are not yet known are included; fights already on the requested
    /// tatami are skipped. Applying everything in one transaction avoids partial assignment.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("/api/tournaments/{tournamentId:guid}/fights/assign-tatami-bulk")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignTatamiBulkAsync(
        Guid tournamentId,
        [FromBody] BulkAssignTatamiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _matchService.AssignTatamiBulkAsync(
            tournamentId, request.Assignments, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Moves a pending fight one position up or down within its tatami's queue.
    /// </summary>
    [HttpPost("queue-move")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MoveInQueueAsync(
        Guid tournamentId,
        Guid fightId,
        [FromBody] MoveFightInQueueRequest request,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.MoveInQueueAsync(fightId, request.Direction, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Starts the fight. Valid only for a pending, non-bye fight with both athletes assigned.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartAsync(
        Guid tournamentId,
        Guid fightId,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.StartAsync(fightId, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Pauses an in-progress fight so it can be resumed later.
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PauseAsync(
        Guid tournamentId,
        Guid fightId,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.PauseAsync(fightId, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Resumes a paused fight.
    /// </summary>
    [HttpPost("resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResumeAsync(
        Guid tournamentId,
        Guid fightId,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.ResumeAsync(fightId, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Adjusts a single score bucket for an in-progress fight.
    /// </summary>
    [HttpPost("score/adjust")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdjustScoreAsync(
        Guid tournamentId,
        Guid fightId,
        [FromBody] AdjustScoreRequest request,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.AdjustScoreAsync(
            fightId, request.Side, request.ScoreType, request.Delta, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Starts osae-komi timing for one side.
    /// </summary>
    [HttpPost("osae-komi/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartOsaeKomiAsync(
        Guid tournamentId,
        Guid fightId,
        [FromBody] OsaeKomiRequest request,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.StartOsaeKomiAsync(fightId, request.Side, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Stops any active osae-komi timing.
    /// </summary>
    [HttpPost("osae-komi/stop")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StopOsaeKomiAsync(
        Guid tournamentId,
        Guid fightId,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.StopOsaeKomiAsync(fightId, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Confirms the winner of an in-progress fight and propagates the result through the bracket.
    /// </summary>
    [HttpPost("result")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmResultAsync(
        Guid tournamentId,
        Guid fightId,
        [FromBody] ConfirmResultRequest request,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.ConfirmResultAsync(fightId, request.WinnerId, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Corrects the winner of an already completed fight. Requires the Admin role.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("correct")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CorrectResultAsync(
        Guid tournamentId,
        Guid fightId,
        [FromBody] CorrectResultRequest request,
        CancellationToken cancellationToken)
    {
        if (!await FightBelongsToTournamentAsync(tournamentId, fightId, cancellationToken)) return NotFound();

        var result = await _matchService.CorrectResultAsync(fightId, request.NewWinnerId, CurrentUser(), cancellationToken);
        return MapResult(result);
    }

    private IActionResult MapResult(MatchActionResult result) => result switch
    {
        MatchActionResult.Success => NoContent(),
        MatchActionResult.FightNotFound => NotFound(),
        MatchActionResult.WinnerNotParticipant => BadRequest(new ProblemDetails
        {
            Title = "Ungültiger Sieger.",
            Detail = "Der angegebene Sieger ist kein Teilnehmer dieses Kampfes.",
            Status = StatusCodes.Status400BadRequest
        }),
        MatchActionResult.InvalidState => Conflict(new ProblemDetails
        {
            Title = "Aktion im aktuellen Zustand nicht möglich.",
            Detail = "Der Kampf befindet sich nicht im richtigen Status für diese Aktion.",
            Status = StatusCodes.Status409Conflict
        }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

    private string CurrentUser()
    {
        var user = User.Identity?.Name;
        return string.IsNullOrWhiteSpace(user) ? "unbekannt" : user;
    }

    private async Task<bool> FightBelongsToTournamentAsync(
        Guid tournamentId,
        Guid fightId,
        CancellationToken cancellationToken)
    {
        var fight = await _fightsStore.GetByIdAsync(fightId, cancellationToken);
        return fight is not null && fight.TournamentId == tournamentId;
    }
}
