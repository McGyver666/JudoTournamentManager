namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Determines whether a derived fight slot receives the winner or loser of its source fight.
/// </summary>
public enum FightSlotSourceOutcome
{
    /// <summary>Uses the winner of the source fight.</summary>
    Winner,

    /// <summary>Uses the loser of the source fight.</summary>
    Loser
}
