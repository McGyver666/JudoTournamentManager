namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// A single fight-to-tatami assignment within a bulk operation.
/// </summary>
public sealed record BulkTatamiAssignment
{
    /// <summary>Identifier of the fight to assign.</summary>
    public Guid FightId { get; init; }

    /// <summary>Target tatami identifier, or <see langword="null"/> to clear the assignment.</summary>
    public Guid? TatamiId { get; init; }
}

/// <summary>
/// Request payload for assigning many fights to tatamis in a single atomic operation.
/// </summary>
/// <remarks>
/// Applying all assignments in one database transaction avoids the concurrent-write
/// contention (SQLite "database is locked") that occurs when the client fires one
/// request per fight in parallel.
/// </remarks>
public sealed record BulkAssignTatamiRequest
{
    /// <summary>The fight-to-tatami assignments to apply.</summary>
    public IReadOnlyList<BulkTatamiAssignment> Assignments { get; init; } = [];
}
