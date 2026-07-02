using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Outcome of a match control operation.
/// </summary>
public enum MatchActionResult
{
    /// <summary>The operation completed successfully.</summary>
    Success,

    /// <summary>The fight was not found.</summary>
    FightNotFound,

    /// <summary>The fight is in a state that does not permit this operation.</summary>
    InvalidState,

    /// <summary>The supplied winner is not one of the fight's participants.</summary>
    WinnerNotParticipant
}

/// <summary>
/// Operates a single fight on a tatami: start, scoring, winner confirmation and correction.
/// Confirming or correcting a winner triggers consistent bracket progression and an audit entry (F-02, F-03, I-01).
/// </summary>
public interface IMatchService
{
    /// <summary>
    /// Assigns a fight to a tatami (or clears the assignment when <paramref name="tatamiId"/> is null).
    /// </summary>
    Task<MatchActionResult> AssignTatamiAsync(
        Guid fightId,
        Guid? tatamiId,
        string user,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the current score and penalties for an in-progress fight.
    /// </summary>
    Task<MatchActionResult> RecordScoreAsync(
        Guid fightId,
        int whiteScore,
        int blueScore,
        int whitePenalties,
        int bluePenalties,
        string user,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts a pending fight. Both athletes must be assigned and the fight must not be a bye.
    /// </summary>
    Task<MatchActionResult> StartAsync(Guid fightId, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Pauses an in-progress fight.
    /// </summary>
    Task<MatchActionResult> PauseAsync(Guid fightId, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Resumes a paused fight.
    /// </summary>
    Task<MatchActionResult> ResumeAsync(Guid fightId, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Adjusts a single score bucket for an in-progress fight.
    /// </summary>
    Task<MatchActionResult> AdjustScoreAsync(
        Guid fightId,
        string side,
        Contracts.ScoreType scoreType,
        int delta,
        string user,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts osae-komi timing for one side.
    /// </summary>
    Task<MatchActionResult> StartOsaeKomiAsync(
        Guid fightId,
        string side,
        string user,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops the active osae-komi timing.
    /// </summary>
    Task<MatchActionResult> StopOsaeKomiAsync(
        Guid fightId,
        string user,
        CancellationToken cancellationToken);

    /// <summary>
    /// Confirms the winner of an in-progress fight, completes it, propagates the result and writes an audit entry.
    /// </summary>
    Task<MatchActionResult> ConfirmResultAsync(
        Guid fightId,
        Guid winnerId,
        string user,
        CancellationToken cancellationToken);

    /// <summary>
    /// Corrects the winner of an already completed fight. The previous winner is preserved in the audit log,
    /// and downstream bracket progression is recalculated consistently (F-03).
    /// </summary>
    Task<MatchActionResult> CorrectResultAsync(
        Guid fightId,
        Guid newWinnerId,
        string user,
        CancellationToken cancellationToken);
}
