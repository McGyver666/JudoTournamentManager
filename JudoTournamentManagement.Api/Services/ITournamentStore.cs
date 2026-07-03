using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Abstraction for tournament persistence in MVP.
/// </summary>
public interface ITournamentStore
{
    /// <summary>
    /// Returns all tournaments.
    /// </summary>
    Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns one tournament by identifier.
    /// </summary>
    Task<Tournament?> GetByIdAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new tournament.
    /// </summary>
    Task<Tournament> CreateAsync(
        string name,
        DateOnly date,
        string venue,
        string organizer,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new tournament with an explicit non-white side color.
    /// </summary>
    Task<Tournament> CreateAsync(
        string name,
        DateOnly date,
        string venue,
        string organizer,
        string accentSideColor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing tournament.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid tournamentId,
        string name,
        DateOnly date,
        string venue,
        string organizer,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing tournament with an explicit non-white side color.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid tournamentId,
        string name,
        DateOnly date,
        string venue,
        string organizer,
        string accentSideColor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a tournament and all its dependent data (tatamis, categories, clubs,
    /// athletes, registrations) in a single transaction.
    /// Returns <c>false</c> if the tournament was not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid tournamentId, CancellationToken cancellationToken);
}
