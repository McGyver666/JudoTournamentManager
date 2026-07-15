using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for club management within a tournament.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/clubs")]
public sealed class ClubsController : ControllerBase
{
    private readonly IClubsStore _clubsStore;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public ClubsController(IClubsStore clubsStore, ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(clubsStore);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _clubsStore = clubsStore;
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Returns all clubs for a tournament, ordered by name.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Club>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Club>>> GetAllAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await _clubsStore.GetAllAsync(tournamentId, cancellationToken));
    }

    /// <summary>
    /// Returns one club by identifier.
    /// </summary>
    [Authorize]
    [HttpGet("{clubId:guid}")]
    [ProducesResponseType(typeof(Club), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Club>> GetByIdAsync(
        Guid tournamentId,
        Guid clubId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var club = await _clubsStore.GetByIdAsync(clubId, cancellationToken);
        if (club is null || club.TournamentId != tournamentId)
        {
            return NotFound();
        }

        return Ok(club);
    }

    /// <summary>
    /// Creates a club within a tournament.
    /// Returns 409 Conflict when a club with the same name already exists.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost]
    [ProducesResponseType(typeof(Club), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Club>> CreateAsync(
        Guid tournamentId,
        [FromBody] CreateClubRequest request,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var created = await _clubsStore.CreateAsync(tournamentId, request.Name, request.ContactName, request.ContactEmail, request.ContactPhone, cancellationToken);
        if (created is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Verein bereits vorhanden.",
                Detail = "Ein Verein mit diesem Namen existiert bereits in diesem Turnier.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return CreatedAtAction(nameof(GetByIdAsync), new { tournamentId, clubId = created.Id }, created);
    }

    /// <summary>
    /// Updates a club.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPut("{clubId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(
        Guid tournamentId,
        Guid clubId,
        [FromBody] UpdateClubRequest request,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var club = await _clubsStore.GetByIdAsync(clubId, cancellationToken);
        if (club is null || club.TournamentId != tournamentId)
        {
            return NotFound();
        }

        await _clubsStore.UpdateAsync(clubId, request.Name, request.ContactName, request.ContactEmail, request.ContactPhone, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a club. Returns 409 Conflict when the club still has athletes assigned.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpDelete("{clubId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(
        Guid tournamentId,
        Guid clubId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var club = await _clubsStore.GetByIdAsync(clubId, cancellationToken);
        if (club is null || club.TournamentId != tournamentId)
        {
            return NotFound();
        }

        if (await _clubsStore.HasAthletesAsync(clubId, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Verein hat Athleten.",
                Detail = "Der Verein kann nicht gelöscht werden, solange ihm Athleten zugeordnet sind.",
                Status = StatusCodes.Status409Conflict
            });
        }

        await _clubsStore.DeleteAsync(clubId, cancellationToken);
        return NoContent();
    }

    private async Task<bool> TournamentExistsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        return tournament is not null;
    }
}
