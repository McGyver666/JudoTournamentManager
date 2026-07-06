using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Abstraction for athlete persistence.
/// </summary>
public interface IAthletesStore
{
    /// <summary>
    /// Returns all athletes for a tournament, ordered by last name then first name.
    /// </summary>
    Task<IReadOnlyList<Athlete>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one athlete by identifier, or <c>null</c> if not found.
    /// </summary>
    Task<Athlete?> GetByIdAsync(Guid athleteId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new athlete.
    /// When <paramref name="allowDuplicate"/> is <c>false</c> (default) and a probable
    /// duplicate (same first name, last name, birth year and club) already exists in the
    /// tournament, returns <c>null</c> to signal the conflict.
    /// When <paramref name="allowDuplicate"/> is <c>true</c> the athlete is always created.
    /// </summary>
    Task<Athlete?> CreateAsync(
        Guid tournamentId,
        Guid clubId,
        string firstName,
        string lastName,
        int birthYear,
        Gender gender,
        string? licenseId,
        decimal? weightKg,
        int grade,
        bool allowDuplicate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing athlete. Returns <c>false</c> if the athlete was not found.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid athleteId,
        Guid clubId,
        string firstName,
        string lastName,
        int birthYear,
        Gender gender,
        string? licenseId,
        decimal? weightKg,
        int grade,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an athlete. Returns <c>false</c> if the athlete was not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid athleteId, CancellationToken cancellationToken);
}
