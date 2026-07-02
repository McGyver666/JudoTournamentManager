using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Read access to fight records (bracket view, fight queue).
/// Write operations are performed by <see cref="IBracketService"/> and the match control service.
/// </summary>
public interface IFightsStore
{
    /// <summary>
    /// Returns all fights for a category, ordered by round then fight number.
    /// </summary>
    Task<IReadOnlyList<Fight>> GetAllAsync(Guid tournamentId, Guid categoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one fight by identifier, or <c>null</c> if not found.
    /// </summary>
    Task<Fight?> GetByIdAsync(Guid fightId, CancellationToken cancellationToken);
}
