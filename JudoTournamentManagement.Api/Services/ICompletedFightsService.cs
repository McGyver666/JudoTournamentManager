using JudoTournamentManagement.Api.Contracts;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Provides the tournament-wide combat overview: all completed (non-bye) fights with resolved
/// athlete, club, category, and tatami names.
/// </summary>
public interface ICompletedFightsService
{
    /// <summary>
    /// Returns every completed, non-bye fight of a tournament, most recently completed first.
    /// </summary>
    /// <param name="tournamentId">Tournament to query.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Enriched fight summaries ordered by completion time descending.</returns>
    Task<IReadOnlyList<CompletedFightSummary>> GetCompletedFightsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken);
}
