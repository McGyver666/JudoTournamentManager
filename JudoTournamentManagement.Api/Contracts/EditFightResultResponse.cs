namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Outcome of an EditResult operation.
/// </summary>
public enum EditResultStatus
{
    /// <summary>Edit applied successfully.</summary>
    Success,

    /// <summary>The fight was not found.</summary>
    FightNotFound,

    /// <summary>The fight cannot be edited (bye, not completed, or group-stage fight).</summary>
    InvalidState,

    /// <summary>The supplied winner ID is not one of the fight's participants.</summary>
    WinnerNotParticipant,

    /// <summary>
    /// Downstream fights that are already started would be affected.
    /// The edit was not applied; re-submit with Confirmed=true to proceed.
    /// </summary>
    ConfirmationRequired
}

/// <summary>
/// Summary of an already-started fight that would be reset by an edit.
/// </summary>
/// <param name="FightId">ID of the affected fight.</param>
/// <param name="CategoryName">Human-readable category name.</param>
/// <param name="Round">Bracket round number.</param>
/// <param name="FightNumber">Fight number within the round.</param>
/// <param name="Status">Current status string (InProgress, Paused, Completed).</param>
public sealed record AffectedFightSummary(
    Guid FightId,
    string CategoryName,
    int Round,
    int FightNumber,
    string Status);

/// <summary>
/// Result returned by the EditResult service operation.
/// </summary>
/// <param name="Status">Outcome of the operation.</param>
/// <param name="AffectedFights">
/// Populated when <see cref="Status"/> is <see cref="EditResultStatus.ConfirmationRequired"/>.
/// Lists already-started downstream fights that would be reset.
/// </param>
public sealed record EditFightResultResponse(
    EditResultStatus Status,
    IReadOnlyList<AffectedFightSummary>? AffectedFights = null);
