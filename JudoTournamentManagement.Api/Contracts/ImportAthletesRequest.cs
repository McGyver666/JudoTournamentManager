using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for importing multiple athletes in a single operation.
/// </summary>
public sealed record ImportAthletesRequest
{
    /// <summary>
    /// Athletes to import.
    /// </summary>
    [Required(ErrorMessage = "Die Athletenliste ist erforderlich.")]
    [MinLength(1, ErrorMessage = "Es muss mindestens ein Athlet importiert werden.")]
    public IReadOnlyList<CreateAthleteRequest> Athletes { get; init; } = [];
}