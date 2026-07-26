namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Direction to move a fight within its tatami's pending queue.
/// </summary>
public enum QueueMoveDirection
{
    /// <summary>Move the fight one position earlier in the queue.</summary>
    Up,

    /// <summary>Move the fight one position later in the queue.</summary>
    Down
}

/// <summary>
/// Request payload for reordering a pending fight within its tatami queue.
/// </summary>
public sealed record MoveFightInQueueRequest
{
    /// <summary>Direction to move the fight.</summary>
    public QueueMoveDirection Direction { get; init; }
}
