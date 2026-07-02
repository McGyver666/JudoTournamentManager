using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Abstraction for tatami persistence.
/// </summary>
public interface ITatamisStore
{
    /// <summary>
    /// Returns all tatamis for a tournament, ordered by <see cref="Tatami.DisplayOrder"/>.
    /// </summary>
    Task<IReadOnlyList<Tatami>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one tatami by identifier, or <c>null</c> if not found.
    /// </summary>
    Task<Tatami?> GetByIdAsync(Guid tatamisId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new tatami. If <paramref name="displayOrder"/> is <c>null</c> the next available
    /// position after existing tatamis is used automatically.
    /// </summary>
    Task<Tatami> CreateAsync(
        Guid tournamentId,
        string name,
        int? displayOrder,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing tatami. Returns <c>false</c> if the tatami was not found.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid tatamisId,
        string name,
        int displayOrder,
        bool isActive,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a tatami. Returns <c>false</c> if the tatami was not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid tatamisId, CancellationToken cancellationToken);
}
