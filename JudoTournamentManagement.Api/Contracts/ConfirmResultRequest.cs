using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for confirming the winner of a fight.
/// </summary>
public sealed record ConfirmResultRequest
{
    /// <summary>Identifier of the winning athlete; must be one of the fight's participants.</summary>
    [Required(ErrorMessage = "Der Sieger ist erforderlich.")]
    public Guid WinnerId { get; init; }
}
