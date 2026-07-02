using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Computes provisional category rankings and tournament medal tables (G-02, G-03).
/// </summary>
public interface IRankingService
{
    /// <summary>
    /// Returns provisional rankings for a single category.
    /// Only placements that can be determined from completed fights are included.
    /// </summary>
    Task<IReadOnlyList<RankingEntry>> GetCategoryRankingsAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the medal table for a tournament, aggregating medals across all categories
    /// and grouping them by club.
    /// </summary>
    Task<IReadOnlyList<MedalEntry>> GetMedalTableAsync(
        Guid tournamentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns round-robin standings for a category, grouped by pool.
    /// Tie-break order: wins → waza-ari scored → yuko scored → fewest shidos.
    /// Returns an empty list for non-round-robin categories.
    /// </summary>
    Task<IReadOnlyList<RoundRobinStanding>> GetRoundRobinStandingsAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken);
}
