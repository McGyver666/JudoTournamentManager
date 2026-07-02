using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Result of an athlete swap request.
/// </summary>
public enum SwapResult
{
    /// <summary>Swap completed successfully.</summary>
    Success,

    /// <summary>At least one non-bye fight has already started or completed; bracket is locked.</summary>
    BracketLocked,

    /// <summary>One or both athletes were not found in the category bracket.</summary>
    AthleteNotInBracket
}

/// <summary>
/// Manages bracket generation and pre-fight adjustments for a category.
/// </summary>
public interface IBracketService
{
    /// <summary>
    /// Generates the draw for a category using a deterministic shuffle seeded on the category ID.
    /// If a bracket already exists for the category it is replaced.
    /// Keeps the category editable after generation. Category lock is applied
    /// when the first real (non-bye) fight is started.
    /// Throws <see cref="InvalidOperationException"/> if fewer than 2 athletes are registered.
    /// </summary>
    Task<IReadOnlyList<Fight>> GenerateAsync(
        Guid tournamentId,
        Guid categoryId,
        BracketFormat format,
        CancellationToken cancellationToken);

    /// <summary>
    /// Swaps two athletes' positions across all fights in the bracket.
    /// Returns <see cref="SwapResult.BracketLocked"/> when any non-bye fight has been started or completed.
    /// Returns <see cref="SwapResult.AthleteNotInBracket"/> when either athlete is not in the bracket.
    /// </summary>
    Task<SwapResult> SwapAthletesAsync(
        Guid categoryId,
        Guid athleteId1,
        Guid athleteId2,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates the knockout phase for a <see cref="BracketFormat.RoundRobinWithKnockout"/> category
    /// after all group-stage fights have been completed.
    /// The top 2 athletes from each pool advance: Pool1 1st vs Pool2 2nd, Pool2 1st vs Pool1 2nd.
    /// Returns false when insufficient data is available.
    /// </summary>
    Task<bool> TryGenerateKnockoutFromGroupStageAsync(
        Guid categoryId,
        IReadOnlyList<(Guid AthleteId, int Pool)> rankedAthletes,
        CancellationToken cancellationToken);
}
