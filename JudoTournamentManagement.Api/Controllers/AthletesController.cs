using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for athlete management within a tournament.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/athletes")]
public sealed class AthletesController : ControllerBase
{
    private readonly IAthletesStore _athletesStore;
    private readonly IClubsStore _clubsStore;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public AthletesController(
        IAthletesStore athletesStore,
        IClubsStore clubsStore,
        ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(athletesStore);
        ArgumentNullException.ThrowIfNull(clubsStore);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _athletesStore = athletesStore;
        _clubsStore = clubsStore;
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Returns all athletes for a tournament, ordered by last name then first name.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Athlete>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Athlete>>> GetAllAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await _athletesStore.GetAllAsync(tournamentId, cancellationToken));
    }

    /// <summary>
    /// Returns one athlete by identifier.
    /// </summary>
    [Authorize]
    [HttpGet("{athleteId:guid}")]
    [ProducesResponseType(typeof(Athlete), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Athlete>> GetByIdAsync(
        Guid tournamentId,
        Guid athleteId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var athlete = await _athletesStore.GetByIdAsync(athleteId, cancellationToken);
        if (athlete is null || athlete.TournamentId != tournamentId)
        {
            return NotFound();
        }

        return Ok(athlete);
    }

    /// <summary>
    /// Creates an athlete within a tournament.
    /// Use <c>?allowDuplicate=true</c> to bypass the duplicate name/birth year/club check.
    /// Returns 409 Conflict when a probable duplicate exists and <c>allowDuplicate</c> is not set.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost]
    [ProducesResponseType(typeof(Athlete), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Athlete>> CreateAsync(
        Guid tournamentId,
        [FromBody] CreateAthleteRequest request,
        [FromQuery] bool allowDuplicate = false,
        CancellationToken cancellationToken = default)
    {
        if (request.BirthYear is null)
        {
            ModelState.AddModelError(nameof(request.BirthYear), "Das Geburtsjahr ist erforderlich.");
            return ValidationProblem(ModelState);
        }

        if (request.Gender is null)
        {
            ModelState.AddModelError(nameof(request.Gender), "Das Geschlecht ist erforderlich.");
            return ValidationProblem(ModelState);
        }

        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var club = await _clubsStore.GetByIdAsync(request.ClubId, cancellationToken);
        if (club is null || club.TournamentId != tournamentId)
        {
            ModelState.AddModelError(nameof(request.ClubId), "Der angegebene Verein wurde nicht gefunden.");
            return ValidationProblem(ModelState);
        }

        var created = await _athletesStore.CreateAsync(
            tournamentId,
            request.ClubId,
            request.FirstName,
            request.LastName,
            request.BirthYear.Value,
            request.Gender.Value,
            request.LicenseId,
            request.WeightKg,
            allowDuplicate,
            cancellationToken);

        if (created is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Mögliches Duplikat gefunden.",
                Detail = "Ein Athlet mit diesem Namen, Jahrgang und Verein existiert möglicherweise bereits. Verwenden Sie ?allowDuplicate=true, um den Athleten trotzdem anzulegen.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return CreatedAtAction(nameof(GetByIdAsync), new { tournamentId, athleteId = created.Id }, created);
    }

    /// <summary>
    /// Updates an athlete.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPut("{athleteId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(
        Guid tournamentId,
        Guid athleteId,
        [FromBody] UpdateAthleteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BirthYear is null)
        {
            ModelState.AddModelError(nameof(request.BirthYear), "Das Geburtsjahr ist erforderlich.");
            return ValidationProblem(ModelState);
        }

        if (request.Gender is null)
        {
            ModelState.AddModelError(nameof(request.Gender), "Das Geschlecht ist erforderlich.");
            return ValidationProblem(ModelState);
        }

        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var athlete = await _athletesStore.GetByIdAsync(athleteId, cancellationToken);
        if (athlete is null || athlete.TournamentId != tournamentId)
        {
            return NotFound();
        }

        var club = await _clubsStore.GetByIdAsync(request.ClubId, cancellationToken);
        if (club is null || club.TournamentId != tournamentId)
        {
            ModelState.AddModelError(nameof(request.ClubId), "Der angegebene Verein wurde nicht gefunden.");
            return ValidationProblem(ModelState);
        }

        await _athletesStore.UpdateAsync(
            athleteId,
            request.ClubId,
            request.FirstName,
            request.LastName,
            request.BirthYear.Value,
            request.Gender.Value,
            request.LicenseId,
            request.WeightKg,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Deletes an athlete.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpDelete("{athleteId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        Guid tournamentId,
        Guid athleteId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var athlete = await _athletesStore.GetByIdAsync(athleteId, cancellationToken);
        if (athlete is null || athlete.TournamentId != tournamentId)
        {
            return NotFound();
        }

        await _athletesStore.DeleteAsync(athleteId, cancellationToken);
        return NoContent();
    }

    private async Task<bool> TournamentExistsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        return tournament is not null;
    }
}
