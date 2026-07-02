using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for swapping two athletes in the bracket.
/// </summary>
public sealed record SwapAthletesRequest
{
    /// <summary>
    /// First athlete to swap.
    /// </summary>
    [Required(ErrorMessage = "Der erste Athlet ist erforderlich.")]
    public Guid AthleteId1 { get; init; }

    /// <summary>
    /// Second athlete to swap.
    /// </summary>
    [Required(ErrorMessage = "Der zweite Athlet ist erforderlich.")]
    public Guid AthleteId2 { get; init; }
}
