using System.Security.Claims;
using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShiaiManager.Api.Controllers;

/// <summary>
/// API endpoints for configuring an NWJV team competition matchday.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/team-matchday")]
public sealed class TeamMatchdaysController : ControllerBase
{
    private readonly ITeamMatchdayStore _teamMatchdayStore;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public TeamMatchdaysController(ITeamMatchdayStore teamMatchdayStore, IAuditLogService auditLogService)
    {
        ArgumentNullException.ThrowIfNull(teamMatchdayStore);
        ArgumentNullException.ThrowIfNull(auditLogService);
        _teamMatchdayStore = teamMatchdayStore;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Returns a team-matchday configuration.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(TeamMatchday), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMatchday>> GetAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var matchday = await _teamMatchdayStore.GetAsync(tournamentId, cancellationToken);
        return matchday is null ? NotFound() : Ok(matchday);
    }

    /// <summary>
    /// Adds a team that represents an existing tournament club.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("teams")]
    [ProducesResponseType(typeof(TeamMatchdayTeam), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMatchdayTeam>> AddTeamAsync(
        Guid tournamentId,
        [FromBody] CreateTeamMatchdayTeamRequest request,
        CancellationToken cancellationToken)
    {
        var team = await _teamMatchdayStore.AddTeamAsync(tournamentId, request.ClubId, request.Name, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        await _auditLogService.LogAsync(
            tournamentId,
            CurrentUser(),
            "TeamMatchdayTeamCreated",
            "TeamMatchdayTeam",
            team.Id,
            $"ClubId={team.ClubId}",
            cancellationToken);

        return Created($"/api/tournaments/{tournamentId}/team-matchday/teams/{team.Id}", team);
    }

    /// <summary>
    /// Draws the shared weight-class order before encounter bouts are prepared.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("weight-class-order/draw")]
    [ProducesResponseType(typeof(IReadOnlyList<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<int>>> DrawWeightClassOrderAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var order = await _teamMatchdayStore.DrawWeightClassOrderAsync(tournamentId, cancellationToken);
        if (order is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Gewichtsklassenreihenfolge kann nicht ausgelost werden.",
                Status = StatusCodes.Status409Conflict
            });
        }

        await _auditLogService.LogAsync(
            tournamentId,
            CurrentUser(),
            "TeamMatchdayWeightClassOrderDrawn",
            "TeamMatchday",
            tournamentId,
            $"Count={order.Count}",
            cancellationToken);
        return Ok(order);
    }

    /// <summary>
    /// Stores a manually selected weight-class order before encounter bouts start.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("weight-class-order")]
    [ProducesResponseType(typeof(IReadOnlyList<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<int>>> SetWeightClassOrderAsync(
        Guid tournamentId,
        [FromBody] SetTeamMatchdayWeightClassOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _teamMatchdayStore.SetWeightClassOrderAsync(tournamentId, request.Order, cancellationToken);
        if (order is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Die Gewichtsklassenreihenfolge kann nicht gespeichert werden.",
                Status = StatusCodes.Status409Conflict
            });
        }

        await _auditLogService.LogAsync(
            tournamentId,
            CurrentUser(),
            "TeamMatchdayWeightClassOrderSet",
            "TeamMatchday",
            tournamentId,
            $"Count={order.Count}",
            cancellationToken);
        return Ok(order);
    }

    /// <summary>
    /// Configures a team encounter on the matchday.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("encounters")]
    [ProducesResponseType(typeof(TeamEncounter), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeamEncounter>> CreateEncounterAsync(
        Guid tournamentId,
        [FromBody] CreateTeamEncounterRequest request,
        CancellationToken cancellationToken)
    {
        var encounter = await _teamMatchdayStore.CreateEncounterAsync(
            tournamentId,
            request.HomeTeamId,
            request.AwayTeamId,
            request.TatamiId,
            cancellationToken);
        if (encounter is null)
        {
            return NotFound();
        }

        await _auditLogService.LogAsync(
            tournamentId,
            CurrentUser(),
            "TeamEncounterCreated",
            "TeamEncounter",
            encounter.Id,
            null,
            cancellationToken);
        return Created($"/api/tournaments/{tournamentId}/team-matchday/encounters/{encounter.Id}", encounter);
    }

    /// <summary>
    /// Deletes an encounter before its first fight has started.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpDelete("encounters/{encounterId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEncounterAsync(
        Guid tournamentId,
        Guid encounterId,
        CancellationToken cancellationToken)
    {
        var result = await _teamMatchdayStore.DeleteEncounterAsync(tournamentId, encounterId, cancellationToken);
        if (!result.Succeeded)
        {
            return Conflict(new ProblemDetails { Title = result.Message, Detail = result.Code, Status = StatusCodes.Status409Conflict });
        }

        await _auditLogService.LogAsync(
            tournamentId,
            CurrentUser(),
            "TeamEncounterDeleted",
            "TeamEncounter",
            encounterId,
            null,
            cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Returns both team lineups for one encounter leg.
    /// </summary>
    [Authorize]
    [HttpGet("encounters/{encounterId:guid}/lineups/{legNumber:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamLineupEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamLineupEntry>>> GetLineupAsync(
        Guid encounterId,
        int legNumber,
        CancellationToken cancellationToken)
    {
        return Ok(await _teamMatchdayStore.GetLineupAsync(encounterId, legNumber, cancellationToken));
    }

    /// <summary>
    /// Replaces one team's lineup before the encounter leg is prepared.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPut("encounters/{encounterId:guid}/lineups")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReplaceLineupAsync(
        Guid tournamentId,
        Guid encounterId,
        [FromBody] ReplaceTeamLineupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _teamMatchdayStore.ReplaceLineupAsync(
            tournamentId,
            encounterId,
            request.LegNumber,
            request.TeamId,
            request.Assignments,
            cancellationToken);
        if (!result.Succeeded)
        {
            return Conflict(new ProblemDetails { Title = result.Message, Detail = result.Code, Status = StatusCodes.Status409Conflict });
        }

        await _auditLogService.LogAsync(
            tournamentId,
            CurrentUser(),
            "TeamEncounterLineupUpdated",
            "TeamEncounter",
            encounterId,
            $"Leg={request.LegNumber};TeamId={request.TeamId}",
            cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Creates the pending standard fights for a complete encounter leg.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("encounters/{encounterId:guid}/prepare")]
    [ProducesResponseType(typeof(IReadOnlyList<EncounterBout>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<EncounterBout>>> PrepareEncounterLegAsync(
        Guid tournamentId,
        Guid encounterId,
        [FromBody] PrepareTeamEncounterLegRequest request,
        CancellationToken cancellationToken)
    {
        var bouts = await _teamMatchdayStore.PrepareEncounterLegAsync(
            tournamentId,
            encounterId,
            request.LegNumber,
            cancellationToken);
        if (bouts is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Der Durchgang kann noch nicht vorbereitet werden.",
                Status = StatusCodes.Status409Conflict
            });
        }

        await _auditLogService.LogAsync(
            tournamentId,
            CurrentUser(),
            "TeamEncounterLegPrepared",
            "TeamEncounter",
            encounterId,
            $"Leg={request.LegNumber};BoutCount={bouts.Count}",
            cancellationToken);
        return Created($"/api/tournaments/{tournamentId}/team-matchday/encounters/{encounterId}/bouts", bouts);
    }

    /// <summary>
    /// Records an encounter no-show before normal bouts are prepared.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("encounters/{encounterId:guid}/no-show")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordNoShowAsync(
        Guid tournamentId,
        Guid encounterId,
        [FromBody] RecordTeamNoShowRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _teamMatchdayStore.RecordNoShowAsync(
            tournamentId,
            encounterId,
            request.NoShowTeamId,
            cancellationToken);
        if (!result.Succeeded)
        {
            return Conflict(new ProblemDetails { Title = result.Message, Detail = result.Code, Status = StatusCodes.Status409Conflict });
        }

        await _auditLogService.LogAsync(
            tournamentId,
            CurrentUser(),
            "TeamEncounterNoShowRecorded",
            "TeamEncounter",
            encounterId,
            $"TeamId={request.NoShowTeamId}",
            cancellationToken);
        return NoContent();
    }

    private string CurrentUser() => User.FindFirstValue(ClaimTypes.Name) ?? "unbekannt";
}