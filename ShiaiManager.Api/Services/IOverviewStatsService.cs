using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Computes aggregate data for the tournament overview.
/// </summary>
public interface IOverviewStatsService
{
    /// <summary>
    /// Returns whole-tournament counts and completed-fight metrics.
    /// </summary>
    Task<TournamentOverviewStats> GetAsync(
        Guid tournamentId,
        CancellationToken cancellationToken);
}