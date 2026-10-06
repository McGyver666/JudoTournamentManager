using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShiaiManager.Api.Controllers;

/// <summary>
/// API endpoints for tournament category preset configuration.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/category-presets")]
public sealed class CategoryPresetsController : ControllerBase
{
    private readonly ICategoryPresetsStore _presetsStore;
    private readonly ITournamentStore _tournamentStore;
    private readonly IRegistrationsStore _registrationsStore;
    private readonly ICategoriesStore _categoriesStore;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public CategoryPresetsController(
        ICategoryPresetsStore presetsStore,
        ITournamentStore tournamentStore,
        IRegistrationsStore registrationsStore,
        ICategoriesStore categoriesStore)
    {
        ArgumentNullException.ThrowIfNull(presetsStore);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        ArgumentNullException.ThrowIfNull(registrationsStore);
        ArgumentNullException.ThrowIfNull(categoriesStore);
        _presetsStore = presetsStore;
        _tournamentStore = tournamentStore;
        _registrationsStore = registrationsStore;
        _categoriesStore = categoriesStore;
    }

    /// <summary>
    /// Returns all category presets for a tournament, with birth years computed
    /// from the tournament date.
    /// </summary>
    [Authorize(Roles = "Admin,Operator,Display,Competition")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryPresetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CategoryPresetResponse>>> GetAllAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var presets = await _presetsStore.GetAllAsync(tournamentId, cancellationToken);
        return Ok(presets.Select(MapToResponse).ToArray());
    }

    /// <summary>
    /// Returns preset warnings: registrations without age group, presets that can never be a natural
    /// age group, and categories whose age group has no preset.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpGet("warnings")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryPresetWarning>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CategoryPresetWarning>>> GetWarningsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        var presets = await _presetsStore.GetAllAsync(tournamentId, cancellationToken);
        var registrations = await _registrationsStore.GetDetailedAsync(tournamentId, cancellationToken);
        var categories = await _categoriesStore.GetAllAsync(tournamentId, cancellationToken);
        return Ok(CategoryPresetWarnings.Build(presets, registrations, categories, tournament.Date.Year));
    }

    /// <summary>
    /// Replaces all category presets for a tournament.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPut]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryPresetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CategoryPresetResponse>>> UpdateAllAsync(
        Guid tournamentId,
        [FromBody] UpdateCategoryPresetsRequest request,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        var tournamentYear = tournament.Date.Year;
        var domainPresets = request.Presets
            .Select((item, index) => new TournamentCategoryPreset(
                Guid.NewGuid(),
                tournamentId,
                item.AgeGroup.Trim(),
                item.Gender,
                item.MaxAgeYears,
                item.MinAgeYears,
                item.MaxAgeYears.HasValue ? tournamentYear - item.MaxAgeYears.Value : (int?)null,
                item.MinAgeYears.HasValue ? tournamentYear - item.MinAgeYears.Value : (int?)null,
                item.DefaultMatchDurationSeconds,
                item.WeightClassLimitsKg,
                index))
            .ToArray();

        await _presetsStore.ReplaceAllAsync(tournamentId, domainPresets, cancellationToken);

        var saved = await _presetsStore.GetAllAsync(tournamentId, cancellationToken);
        return Ok(saved.Select(MapToResponse).ToArray());
    }

    /// <summary>
    /// Resets presets to the default DJB/NWJV standard classes based on the tournament year.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("reset-defaults")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryPresetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CategoryPresetResponse>>> ResetDefaultsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        // Delete existing and re-seed
        await _presetsStore.ReplaceAllAsync(tournamentId, [], cancellationToken);
        await _presetsStore.SeedDefaultsAsync(tournamentId, tournament.Date.Year, cancellationToken);

        var presets = await _presetsStore.GetAllAsync(tournamentId, cancellationToken);
        return Ok(presets.Select(MapToResponse).ToArray());
    }

    private static CategoryPresetResponse MapToResponse(TournamentCategoryPreset preset) =>
        new(preset.Id,
            preset.AgeGroup,
            preset.Gender,
            preset.MaxAgeYears,
            preset.MinAgeYears,
            preset.MinBirthYear,
            preset.MaxBirthYear,
            preset.DefaultMatchDurationSeconds,
            preset.WeightClassLimitsKg,
            preset.SortOrder);

    private async Task<bool> TournamentExistsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var t = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        return t is not null;
    }
}
