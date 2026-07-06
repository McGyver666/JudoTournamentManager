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
    private readonly IDm4AthleteImportParser _dm4AthleteImportParser;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public AthletesController(
        IAthletesStore athletesStore,
        IClubsStore clubsStore,
        IDm4AthleteImportParser dm4AthleteImportParser,
        ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(athletesStore);
        ArgumentNullException.ThrowIfNull(clubsStore);
        ArgumentNullException.ThrowIfNull(dm4AthleteImportParser);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _athletesStore = athletesStore;
        _clubsStore = clubsStore;
        _dm4AthleteImportParser = dm4AthleteImportParser;
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

        if (request.Grade is null)
        {
            ModelState.AddModelError(nameof(request.Grade), "Der Gürtelgrad ist erforderlich.");
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
            request.Grade.Value,
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
    /// Imports multiple athletes within a tournament in a single operation.
    /// Use <c>?allowDuplicate=true</c> to bypass duplicate name/birth year/club checks.
    /// Returns 409 Conflict when a probable duplicate exists and <c>allowDuplicate</c> is not set.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("import")]
    [ProducesResponseType(typeof(IReadOnlyList<Athlete>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<Athlete>>> ImportAsync(
        Guid tournamentId,
        [FromBody] ImportAthletesRequest request,
        [FromQuery] bool allowDuplicate = false,
        CancellationToken cancellationToken = default)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        if (request.Athletes.Count == 0)
        {
            ModelState.AddModelError(nameof(request.Athletes), "Es muss mindestens ein Athlet importiert werden.");
            return ValidationProblem(ModelState);
        }

        for (var i = 0; i < request.Athletes.Count; i++)
        {
            var athlete = request.Athletes[i];

            if (athlete.BirthYear is null)
            {
                ModelState.AddModelError($"Athletes[{i}].BirthYear", "Das Geburtsjahr ist erforderlich.");
            }

            if (athlete.Gender is null)
            {
                ModelState.AddModelError($"Athletes[{i}].Gender", "Das Geschlecht ist erforderlich.");
            }

            if (athlete.Grade is null)
            {
                ModelState.AddModelError($"Athletes[{i}].Grade", "Der Gürtelgrad ist erforderlich.");
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var clubs = await _clubsStore.GetAllAsync(tournamentId, cancellationToken);
        var clubIds = clubs.Select(x => x.Id).ToHashSet();

        for (var i = 0; i < request.Athletes.Count; i++)
        {
            if (!clubIds.Contains(request.Athletes[i].ClubId))
            {
                ModelState.AddModelError($"Athletes[{i}].ClubId", "Der angegebene Verein wurde nicht gefunden.");
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var importItems = request.Athletes
            .Select(x => new AthleteImportItem(
                x.ClubId,
                x.FirstName,
                x.LastName,
                x.BirthYear!.Value,
                x.Gender!.Value,
                x.LicenseId,
                x.WeightKg,
                x.Grade!.Value))
            .ToArray();

        var created = await _athletesStore.CreateBulkAsync(
            tournamentId,
            importItems,
            allowDuplicate,
            cancellationToken);

        if (created is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Mögliches Duplikat gefunden.",
                Detail = "Mindestens ein Athlet aus dem Import existiert mit Name, Jahrgang und Verein möglicherweise bereits. Verwenden Sie ?allowDuplicate=true, um den Import trotzdem auszuführen.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return Ok(created);
    }

    /// <summary>
    /// Imports athletes from an NWJV E-Melder DM4 file.
    /// The file must contain one club in <c>[Vereine]</c> and athlete rows in <c>[Teilnehmer]</c>.
    /// Use <c>?allowDuplicate=true</c> to bypass duplicate name/birth year/club checks.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("import/dm4")]
    [ProducesResponseType(typeof(IReadOnlyList<Athlete>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<Athlete>>> ImportFromDm4Async(
        Guid tournamentId,
        [FromForm] IFormFile? file,
        [FromQuery] bool allowDuplicate = false,
        CancellationToken cancellationToken = default)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "Es muss eine .dm4-Datei hochgeladen werden.");
            return ValidationProblem(ModelState);
        }

        if (!Path.GetExtension(file.FileName).Equals(".dm4", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(file), "Es sind nur Dateien mit der Endung .dm4 erlaubt.");
            return ValidationProblem(ModelState);
        }

        byte[] fileBytes;
        await using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream, cancellationToken);
            fileBytes = memoryStream.ToArray();
        }

        Dm4AthleteImportData parsed;
        try
        {
            parsed = _dm4AthleteImportParser.Parse(fileBytes);
        }
        catch (Dm4ImportParseException ex)
        {
            ModelState.AddModelError(nameof(file), ex.Message);
            return ValidationProblem(ModelState);
        }

        var clubs = await _clubsStore.GetAllAsync(tournamentId, cancellationToken);
        var targetClub = clubs.FirstOrDefault(
            x => string.Equals(x.Name, parsed.ClubName, StringComparison.OrdinalIgnoreCase));

        if (targetClub is null)
        {
            targetClub = await _clubsStore.CreateAsync(tournamentId, parsed.ClubName, cancellationToken);

            // In case of a concurrent create, lookup the existing club by name.
            if (targetClub is null)
            {
                clubs = await _clubsStore.GetAllAsync(tournamentId, cancellationToken);
                targetClub = clubs.FirstOrDefault(
                    x => string.Equals(x.Name, parsed.ClubName, StringComparison.OrdinalIgnoreCase));
            }

            if (targetClub is null)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Verein konnte nicht angelegt werden.",
                    Detail = "Der Verein aus der DM4-Datei konnte nicht ermittelt oder angelegt werden.",
                    Status = StatusCodes.Status409Conflict
                });
            }
        }

        var importItems = parsed.Athletes
            .Select(x => new AthleteImportItem(
                targetClub.Id,
                x.FirstName,
                x.LastName,
                x.BirthYear,
                parsed.Gender,
                null,
                x.WeightKg,
                x.Grade))
            .ToArray();

        var created = await _athletesStore.CreateBulkAsync(
            tournamentId,
            importItems,
            allowDuplicate,
            cancellationToken);

        if (created is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Mögliches Duplikat gefunden.",
                Detail = "Mindestens ein Athlet aus dem Import existiert mit Name, Jahrgang und Verein möglicherweise bereits. Verwenden Sie ?allowDuplicate=true, um den Import trotzdem auszuführen.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return Ok(created);
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

        if (request.Grade is null)
        {
            ModelState.AddModelError(nameof(request.Grade), "Der Gürtelgrad ist erforderlich.");
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
            request.Grade.Value,
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
