using System.Text;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for athlete registration management within a tournament.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/registrations")]
public sealed class RegistrationsController : ControllerBase
{
    private readonly IRegistrationsStore _registrationsStore;
    private readonly IAthletesStore _athletesStore;
    private readonly ICategoriesStore _categoriesStore;
    private readonly ITournamentStore _tournamentStore;
    private readonly IBracketService _bracketService;
    private readonly IDokumePassParser _dokumePassParser;
    private readonly ILogger<RegistrationsController> _logger;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public RegistrationsController(
        IRegistrationsStore registrationsStore,
        IAthletesStore athletesStore,
        ICategoriesStore categoriesStore,
        ITournamentStore tournamentStore,
        IBracketService bracketService,
        IDokumePassParser dokumePassParser,
        ILogger<RegistrationsController> logger)
    {
        ArgumentNullException.ThrowIfNull(registrationsStore);
        ArgumentNullException.ThrowIfNull(athletesStore);
        ArgumentNullException.ThrowIfNull(categoriesStore);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        ArgumentNullException.ThrowIfNull(bracketService);
        ArgumentNullException.ThrowIfNull(dokumePassParser);
        ArgumentNullException.ThrowIfNull(logger);
        _registrationsStore = registrationsStore;
        _athletesStore = athletesStore;
        _categoriesStore = categoriesStore;
        _tournamentStore = tournamentStore;
        _bracketService = bracketService;
        _dokumePassParser = dokumePassParser;
        _logger = logger;
    }

    /// <summary>
    /// Returns all registrations for a tournament with full athlete and category details.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RegistrationDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RegistrationDetail>>> GetAllAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await _registrationsStore.GetDetailedAsync(tournamentId, cancellationToken));
    }

    /// <summary>
    /// Returns one registration by identifier.
    /// </summary>
    [Authorize]
    [HttpGet("{registrationId:guid}")]
    [ProducesResponseType(typeof(Registration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Registration>> GetByIdAsync(
        Guid tournamentId,
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var registration = await _registrationsStore.GetByIdAsync(registrationId, cancellationToken);
        if (registration is null || registration.TournamentId != tournamentId)
        {
            return NotFound();
        }

        return Ok(registration);
    }

    /// <summary>
    /// Exports all registrations for the tournament as a semicolon-separated CSV file.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportCsvAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var registrations = await _registrationsStore.GetDetailedAsync(tournamentId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Kategorie;Altersklasse;Geschlecht;Gewichtsklasse;Nachname;Vorname;Jahrgang;Verein;Lizenznummer;Gewicht;Lizenzbest");

        foreach (var r in registrations)
        {
            var categoryLabel = r.CategoryName ?? "(keine)";
            var ageGroupLabel = r.CategoryAgeGroup ?? "-";
            var genderLabel = r.CategoryGender switch
            {
                Gender.Male => "Männlich",
                Gender.Female => "Weiblich",
                Gender.Mixed => "Gemischt",
                _ => "-"
            };
            var weightLabel = r.CategoryWeightClassKg.HasValue
                ? $"-{r.CategoryWeightClassKg:0.##} kg"
                : "Open";
            var athleteWeight = r.AthleteWeightKg.HasValue
                ? $"{r.AthleteWeightKg:0.##}"
                : "-";
            var licenseConfirmed = r.LicenseConfirmed ? "Ja" : "Nein";

            sb.AppendLine(string.Join(";",
                EscapeCsv(categoryLabel),
                EscapeCsv(ageGroupLabel),
                EscapeCsv(genderLabel),
                EscapeCsv(weightLabel),
                EscapeCsv(r.AthleteLastName),
                EscapeCsv(r.AthleteFirstName),
                r.AthleteBirthYear.ToString(),
                EscapeCsv(r.AthleteClubName),
                EscapeCsv(r.AthleteLicenseId ?? string.Empty),
                EscapeCsv(athleteWeight),
                EscapeCsv(licenseConfirmed)));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", "waage.csv");
    }

    /// <summary>
    /// Registers an athlete for the tournament with weight-in and license confirmation.
    /// Category assignment happens later via AssignCategoryAsync endpoint.
    /// Returns 409 Conflict when the athlete is already registered in this tournament.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost]
    [ProducesResponseType(typeof(Registration), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Registration>> CreateAsync(
        Guid tournamentId,
        [FromBody] CreateRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var athlete = await _athletesStore.GetByIdAsync(request.AthleteId, cancellationToken);
        if (athlete is null || athlete.TournamentId != tournamentId)
        {
            ModelState.AddModelError(nameof(request.AthleteId), "Der angegebene Athlet wurde nicht gefunden.");
            return ValidationProblem(ModelState);
        }

        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        // If DokuMe QR URL provided, use license-aware registration flow
        if (!string.IsNullOrEmpty(request.DokumeQrUrl))
        {
            var created = await _registrationsStore.CreateWithLicenseCheckAsync(
                tournamentId,
                request.AthleteId,
                request.WeightKg,
                request.LicenseId,
                request.LicenseConfirmed,
                request.DokumeQrUrl,
                request.LicenseCheckOverrideReason,
                _dokumePassParser,
                tournament.Date,
                User.Identity?.Name ?? "system",
                cancellationToken);

            if (created is null)
            {
                if (await _registrationsStore.GetByIdAsync(request.AthleteId, cancellationToken) is not null)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Athlet bereits angemeldet.",
                        Detail = "Der Athlet ist in diesem Turnier bereits angemeldet.",
                        Status = StatusCodes.Status409Conflict
                    });
                }

                ModelState.AddModelError(
                    nameof(request.DokumeQrUrl),
                    "Lizenzenprüfung fehlgeschlagen. Überprüfen Sie die QR-Code-Daten.");
                return ValidationProblem(ModelState);
            }

            return CreatedAtAction(
                nameof(GetByIdAsync),
                new { tournamentId, registrationId = created.Id },
                created);
        }

        // No QR URL: use standard registration flow
        var registration = await _registrationsStore.CreateAsync(
            tournamentId,
            request.AthleteId,
            request.WeightKg,
            request.LicenseId,
            request.LicenseConfirmed,
            cancellationToken);

        if (registration is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Athlet bereits angemeldet.",
                Detail = "Der Athlet ist in diesem Turnier bereits angemeldet.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return CreatedAtAction(
            nameof(GetByIdAsync),
            new { tournamentId, registrationId = registration.Id },
            registration);
    }

    /// <summary>
    /// Unregisters an athlete (deletes a registration).
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpDelete("{registrationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        Guid tournamentId,
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var registration = await _registrationsStore.GetByIdAsync(registrationId, cancellationToken);
        if (registration is null || registration.TournamentId != tournamentId)
        {
            return NotFound();
        }

        await _registrationsStore.DeleteAsync(registrationId, cancellationToken);

        if (registration.CategoryId.HasValue)
        {
            await RefreshCategoryDrawIfPossibleAsync(
                tournamentId,
                registration.CategoryId.Value,
                cancellationToken);
        }

        return NoContent();
    }

    /// <summary>
    /// Automatically assigns all unassigned registrations to the best-fitting unlocked category
    /// based on gender, birth year, and weight. Already-assigned registrations are skipped.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("auto-assign")]
    [ProducesResponseType(typeof(AutoAssignResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AutoAssignResult>> AutoAssignAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var beforeAssignments = (await _registrationsStore.GetDetailedAsync(tournamentId, cancellationToken))
            .ToDictionary(x => x.Id, x => x.CategoryId);

        var result = await _registrationsStore.AutoAssignAsync(tournamentId, cancellationToken);

        if (result.AssignedCount > 0)
        {
            var afterAssignments = await _registrationsStore.GetDetailedAsync(tournamentId, cancellationToken);
            var affectedCategoryIds = new HashSet<Guid>();

            foreach (var registration in afterAssignments)
            {
                beforeAssignments.TryGetValue(registration.Id, out var oldCategoryId);
                var newCategoryId = registration.CategoryId;
                if (oldCategoryId == newCategoryId)
                {
                    continue;
                }

                if (oldCategoryId.HasValue)
                {
                    affectedCategoryIds.Add(oldCategoryId.Value);
                }

                if (newCategoryId.HasValue)
                {
                    affectedCategoryIds.Add(newCategoryId.Value);
                }
            }

            await RefreshAffectedDrawsAsync(tournamentId, affectedCategoryIds, cancellationToken);
        }

        return Ok(result);
    }

    /// <summary>
    /// Assigns a registered athlete to a category.
    /// Returns 409 Conflict if the athlete is not yet registered or the category is locked.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("{registrationId:guid}/category")]
    [ProducesResponseType(typeof(Registration), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Registration>> AssignCategoryAsync(
        Guid tournamentId,
        Guid registrationId,
        [FromBody] AssignCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var registration = await _registrationsStore.GetByIdAsync(registrationId, cancellationToken);
        if (registration is null || registration.TournamentId != tournamentId)
        {
            return NotFound();
        }

        var category = await _categoriesStore.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null || category.TournamentId != tournamentId)
        {
            ModelState.AddModelError(nameof(request.CategoryId), "Die angegebene Kategorie wurde nicht gefunden.");
            return ValidationProblem(ModelState);
        }

        if (category.IsLocked)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Kategorie ist gesperrt (Kampf bereits gestartet).",
                Detail = "Die Kategorie ist gesperrt, weil der erste Kampf bereits gestartet wurde. Eine Zuweisung ist nicht mehr möglich.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var oldCategoryId = registration.CategoryId;

        var updated = await _registrationsStore.AssignCategoryAsync(
            registrationId, request.CategoryId, cancellationToken);

        if (updated is null)
        {
            return NotFound();
        }

        var affectedCategoryIds = new HashSet<Guid> { request.CategoryId };
        if (oldCategoryId.HasValue)
        {
            affectedCategoryIds.Add(oldCategoryId.Value);
        }

        await RefreshAffectedDrawsAsync(tournamentId, affectedCategoryIds, cancellationToken);

        return Ok(updated);
    }

    private async Task RefreshAffectedDrawsAsync(
        Guid tournamentId,
        IEnumerable<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        foreach (var categoryId in categoryIds.Distinct())
        {
            await RefreshCategoryDrawIfPossibleAsync(tournamentId, categoryId, cancellationToken);
        }
    }

    private async Task RefreshCategoryDrawIfPossibleAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var category = await _categoriesStore.GetByIdAsync(categoryId, cancellationToken);
        if (category is null
            || category.TournamentId != tournamentId
            || category.IsLocked
            || category.DrawFormat is null)
        {
            return;
        }

        try
        {
            await _bracketService.GenerateAsync(
                tournamentId,
                categoryId,
                category.DrawFormat.Value,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(
                ex,
                "Automatic draw refresh skipped for category {CategoryId} in tournament {TournamentId}.",
                categoryId,
                tournamentId);
        }
    }

    private async Task<bool> TournamentExistsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        return tournament is not null;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
