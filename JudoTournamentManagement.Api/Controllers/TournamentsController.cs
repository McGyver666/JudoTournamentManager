using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for tournament administration.
/// </summary>
[ApiController]
[Route("api/tournaments")]
public sealed class TournamentsController : ControllerBase
{
    private readonly ITournamentStore _tournamentStore;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public TournamentsController(ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Returns all tournaments.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Tournament>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Tournament>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var tournaments = await _tournamentStore.GetAllAsync(cancellationToken);
        return Ok(tournaments);
    }

    /// <summary>
    /// Returns one tournament by identifier.
    /// </summary>
    [Authorize]
    [HttpGet("{tournamentId:guid}")]
    [ProducesResponseType(typeof(Tournament), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Tournament>> GetByIdAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        return Ok(tournament);
    }

    /// <summary>
    /// Creates a tournament.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost]
    [ProducesResponseType(typeof(Tournament), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Tournament>> CreateAsync(
        [FromBody] CreateTournamentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Date is null)
        {
            ModelState.AddModelError(nameof(request.Date), "Das Datum ist erforderlich.");
            return ValidationProblem(ModelState);
        }

        var created = await _tournamentStore.CreateAsync(
            request.Name,
            request.Date.Value,
            request.Venue,
            request.Organizer,
            request.AccentSideColor,
            cancellationToken);

        return Created($"/api/tournaments/{created.Id}", created);
    }

    /// <summary>
    /// Updates a tournament.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPut("{tournamentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(
        Guid tournamentId,
        [FromBody] UpdateTournamentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Date is null)
        {
            ModelState.AddModelError(nameof(request.Date), "Das Datum ist erforderlich.");
            return ValidationProblem(ModelState);
        }

        var updated = await _tournamentStore.UpdateAsync(
            tournamentId,
            request.Name,
            request.Date.Value,
            request.Venue,
            request.Organizer,
            request.AccentSideColor,
            cancellationToken);

        return updated ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes a tournament and all its dependent data (tatamis, categories, clubs, athletes, registrations).
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpDelete("{tournamentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var deleted = await _tournamentStore.DeleteAsync(tournamentId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
