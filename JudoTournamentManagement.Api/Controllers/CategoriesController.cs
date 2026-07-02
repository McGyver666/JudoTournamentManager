using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for category (Alters-/Gewichtsklasse) management within a tournament.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoriesStore _categoriesStore;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public CategoriesController(ICategoriesStore categoriesStore, ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(categoriesStore);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _categoriesStore = categoriesStore;
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Returns all categories for a tournament, ordered by age group then name.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Category>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Category>>> GetAllAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var categories = await _categoriesStore.GetAllAsync(tournamentId, cancellationToken);
        return Ok(categories);
    }

    /// <summary>
    /// Returns one category by identifier.
    /// </summary>
    [Authorize]
    [HttpGet("{categoryId:guid}")]
    [ProducesResponseType(typeof(Category), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Category>> GetByIdAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var category = await _categoriesStore.GetByIdAsync(categoryId, cancellationToken);
        if (category is null || category.TournamentId != tournamentId)
        {
            return NotFound();
        }

        return Ok(category);
    }

    /// <summary>
    /// Creates a category within a tournament.
    /// Returns 409 Conflict when a category with the same age group, gender and weight class already exists.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost]
    [ProducesResponseType(typeof(Category), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Category>> CreateAsync(
        Guid tournamentId,
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Gender is null)
        {
            ModelState.AddModelError(nameof(request.Gender), "Das Geschlecht ist erforderlich.");
            return ValidationProblem(ModelState);
        }

        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var created = await _categoriesStore.CreateAsync(
            tournamentId,
            request.Name,
            request.AgeGroup,
            request.Gender.Value,
            request.WeightClassKg,
            request.MinBirthYear,
            request.MaxBirthYear,
            request.RulesetNotes,
            request.MatchDurationSeconds,
            request.GoldenScoreEnabled,
            request.GoldenScoreDurationSeconds,
            cancellationToken);

        if (created is null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Kategorie bereits vorhanden.",
                Detail = "Eine Kategorie mit dieser Altersklasse, diesem Geschlecht und dieser Gewichtsklasse existiert bereits in diesem Turnier.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return CreatedAtAction(
            nameof(GetByIdAsync),
            new { tournamentId, categoryId = created.Id },
            created);
    }

    /// <summary>
    /// Updates a category.
    /// Returns 409 Conflict when the category is locked.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPut("{categoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(
        Guid tournamentId,
        Guid categoryId,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Gender is null)
        {
            ModelState.AddModelError(nameof(request.Gender), "Das Geschlecht ist erforderlich.");
            return ValidationProblem(ModelState);
        }

        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var category = await _categoriesStore.GetByIdAsync(categoryId, cancellationToken);
        if (category is null || category.TournamentId != tournamentId)
        {
            return NotFound();
        }

        if (category.IsLocked)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Kategorie ist gesperrt.",
                Detail = "Die Kategorie ist gesperrt und kann nicht mehr geändert werden.",
                Status = StatusCodes.Status409Conflict
            });
        }

        await _categoriesStore.UpdateAsync(
            categoryId,
            request.Name,
            request.AgeGroup,
            request.Gender.Value,
            request.WeightClassKg,
            request.MinBirthYear,
            request.MaxBirthYear,
            request.RulesetNotes,
            request.MatchDurationSeconds,
            request.GoldenScoreEnabled,
            request.GoldenScoreDurationSeconds,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Deletes a category.
    /// Returns 409 Conflict when the category is locked.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpDelete("{categoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var category = await _categoriesStore.GetByIdAsync(categoryId, cancellationToken);
        if (category is null || category.TournamentId != tournamentId)
        {
            return NotFound();
        }

        if (category.IsLocked)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Kategorie ist gesperrt.",
                Detail = "Die Kategorie ist gesperrt und kann nicht gelöscht werden.",
                Status = StatusCodes.Status409Conflict
            });
        }

        await _categoriesStore.DeleteAsync(categoryId, cancellationToken);
        return NoContent();
    }

    private async Task<bool> TournamentExistsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        return tournament is not null;
    }
}
