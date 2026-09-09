using System.ComponentModel.DataAnnotations;

namespace ShiaiManager.Api.Contracts;

/// <summary>
/// Request payload for confirming the winner of a fight.
/// </summary>
public sealed record ConfirmResultRequest
{
    /// <summary>Identifier of the winning athlete; null records a Hiki-wake for team-matchday fights.</summary>
    public Guid? WinnerId { get; init; }
}
