namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for assigning a fight to a tatami.
/// </summary>
public sealed record AssignTatamiRequest
{
    /// <summary>Target tatami identifier, or null to clear the assignment.</summary>
    public Guid? TatamiId { get; init; }
}
