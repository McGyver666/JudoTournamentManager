using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Abstraction for category persistence.
/// </summary>
public interface ICategoriesStore
{
    /// <summary>
    /// Returns all categories for a tournament, ordered by age group then name.
    /// </summary>
    Task<IReadOnlyList<Category>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one category by identifier, or <c>null</c> if not found.
    /// </summary>
    Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new category. Returns <c>null</c> when a category with the same
    /// age group, gender and weight class already exists for the tournament.
    /// </summary>
    Task<Category?> CreateAsync(
        Guid tournamentId,
        string name,
        string ageGroup,
        Gender gender,
        decimal? weightClassKg,
        int? minBirthYear,
        int? maxBirthYear,
        string? rulesetNotes,
        int matchDurationSeconds,
        bool goldenScoreEnabled,
        int goldenScoreDurationSeconds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing category. Returns <c>false</c> if the category was not found.
    /// Callers must ensure the category is not locked before calling this method.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid categoryId,
        string name,
        string ageGroup,
        Gender gender,
        decimal? weightClassKg,
        int? minBirthYear,
        int? maxBirthYear,
        string? rulesetNotes,
        int matchDurationSeconds,
        bool goldenScoreEnabled,
        int goldenScoreDurationSeconds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a category. Returns <c>false</c> if the category was not found.
    /// Callers must ensure the category is not locked before calling this method.
    /// </summary>
    Task<bool> DeleteAsync(Guid categoryId, CancellationToken cancellationToken);
}
