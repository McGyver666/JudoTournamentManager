namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Bracket format used when generating the draw for a category.
/// </summary>
public enum BracketFormat
{
    /// <summary>Standard single elimination. No consolation fight.</summary>
    SingleElimination,

    /// <summary>
    /// Single elimination with a 3rd-place consolation fight between the two semi-final losers.
    /// Common in German local judo tournaments.
    /// </summary>
    SingleEliminationWithRepechage,

    /// <summary>
    /// Pure round-robin: every athlete fights every other athlete once.
    /// Final ranking is determined by wins, then waza-ari, yuko, fewest shidos.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Group stage (two pools, round-robin within each pool) followed by a
    /// 4-athlete single-elimination knockout bracket. Top 2 per pool advance.
    /// </summary>
    RoundRobinWithKnockout
}
