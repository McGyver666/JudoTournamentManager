namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Lifecycle state of a single fight.
/// </summary>
public enum FightStatus
{
    /// <summary>Fight has not started yet.</summary>
    Pending,

    /// <summary>Fight is currently in progress on a tatami.</summary>
    InProgress,

    /// <summary>Fight is paused and can be resumed later.</summary>
    Paused,

    /// <summary>Fight has been completed and a winner recorded.</summary>
    Completed
}
