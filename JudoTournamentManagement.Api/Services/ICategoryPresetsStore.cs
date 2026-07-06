using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Abstraction for tournament category preset persistence.
/// </summary>
public interface ICategoryPresetsStore
{
    /// <summary>
    /// Returns all presets for a tournament, ordered by sort order.
    /// Birth years are computed from the tournament's date so they reflect the correct year.
    /// </summary>
    Task<IReadOnlyList<TournamentCategoryPreset>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Seeds default DJB/NWJV presets for a newly created tournament.
    /// Birth years are computed from <paramref name="tournamentYear"/>.
    /// Does nothing if presets already exist for the tournament.
    /// </summary>
    Task SeedDefaultsAsync(Guid tournamentId, int tournamentYear, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces all presets for a tournament (delete-all + insert-all).
    /// </summary>
    Task ReplaceAllAsync(Guid tournamentId, IReadOnlyList<TournamentCategoryPreset> presets, CancellationToken cancellationToken);
}
