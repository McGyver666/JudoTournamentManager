using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for tournament category preset configuration.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/category-presets")]
public sealed class CategoryPresetsController : ControllerBase
{
    private readonly ICategoryPresetsStore _presetsStore;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public CategoryPresetsController(
        ICategoryPresetsStore presetsStore,
        ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(presetsStore);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _presetsStore = presetsStore;
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Returns all category presets for a tournament, with birth years computed
    /// from the tournament date.
    /// </summary>
    [Authorize(Roles = "Admin,Operator,Display")]
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
