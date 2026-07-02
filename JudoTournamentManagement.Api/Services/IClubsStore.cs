using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Abstraction for club persistence.
/// </summary>
public interface IClubsStore
{
    /// <summary>
    /// Returns all clubs for a tournament, ordered by name.
    /// </summary>
    Task<IReadOnlyList<Club>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one club by identifier, or <c>null</c> if not found.
    /// </summary>
    Task<Club?> GetByIdAsync(Guid clubId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new club. Returns <c>null</c> when a club with the same name already
    /// exists within the tournament (case-insensitive).
    /// </summary>
    Task<Club?> CreateAsync(Guid tournamentId, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing club. Returns <c>false</c> if the club was not found.
    /// </summary>
    Task<bool> UpdateAsync(Guid clubId, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a club. Returns <c>false</c> if the club was not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid clubId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns <c>true</c> if any athletes in the tournament are assigned to this club.
    /// </summary>
    Task<bool> HasAthletesAsync(Guid clubId, CancellationToken cancellationToken);
}
