using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for correcting the winner of an already completed fight.
/// </summary>
public sealed record CorrectResultRequest
{
    /// <summary>Identifier of the corrected winning athlete; must be one of the fight's participants.</summary>
    [Required(ErrorMessage = "Der korrigierte Sieger ist erforderlich.")]
    public Guid NewWinnerId { get; init; }
}
