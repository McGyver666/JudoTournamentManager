namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Indicates which part of the bracket a fight belongs to.
/// </summary>
public enum FightBracketType
{
    /// <summary>Main elimination bracket.</summary>
    Main,

    /// <summary>3rd-place consolation fight between the two semi-final losers.</summary>
    Repechage,

    /// <summary>Group-stage fight in a RoundRobinWithKnockout bracket.</summary>
    GroupStage
}
