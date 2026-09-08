using System.ComponentModel.DataAnnotations;
using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Contracts;

/// <summary>
/// Request payload for adding a team to a team matchday.
/// </summary>
public sealed record CreateTeamMatchdayTeamRequest
{
    /// <summary>Existing club represented by the team.</summary>
    [Required(ErrorMessage = "Der Verein ist erforderlich.")]
    public Guid ClubId { get; init; }

    /// <summary>Team display name.</summary>
    [Required(ErrorMessage = "Der Mannschaftsname ist erforderlich.")]
    [StringLength(120, ErrorMessage = "Der Mannschaftsname darf maximal 120 Zeichen lang sein.")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Request payload for confirming an athlete's actual matchday weight.
/// </summary>
public sealed record ConfirmMatchdayWeighInRequest
{
    /// <summary>Athlete whose actual weight is being confirmed.</summary>
    [Required(ErrorMessage = "Der Athlet ist erforderlich.")]
    public Guid AthleteId { get; init; }

    /// <summary>Actual body weight in kilograms.</summary>
    [Range(1, 300, ErrorMessage = "Das Gewicht muss zwischen 1 und 300 kg liegen.")]
    public decimal WeightKg { get; init; }
}

/// <summary>
/// Request payload for configuring a pairing of two matchday teams.
/// </summary>
public sealed record CreateTeamEncounterRequest
{
    [Required(ErrorMessage = "Die Heimmannschaft ist erforderlich.")]
    public Guid HomeTeamId { get; init; }

    [Required(ErrorMessage = "Die Gastmannschaft ist erforderlich.")]
    public Guid AwayTeamId { get; init; }

    public Guid? TatamiId { get; init; }
}

/// <summary>
/// Request payload for assigning a team's athletes to an encounter leg.
/// </summary>
public sealed record ReplaceTeamLineupRequest
{
    [Range(1, 2, ErrorMessage = "Der Durchgang muss 1 oder 2 sein.")]
    public int LegNumber { get; init; }

    [Required(ErrorMessage = "Die Mannschaft ist erforderlich.")]
    public Guid TeamId { get; init; }

    [Required(ErrorMessage = "Die Aufstellung ist erforderlich.")]
    public IReadOnlyList<TeamLineupAssignment> Assignments { get; init; } = [];
}

/// <summary>
/// Request payload for preparing all bouts in one encounter leg.
/// </summary>
public sealed record PrepareTeamEncounterLegRequest
{
    [Range(1, 2, ErrorMessage = "Der Durchgang muss 1 oder 2 sein.")]
    public int LegNumber { get; init; }
}

/// <summary>
/// Request payload for marking one team as not appearing for an encounter.
/// </summary>
public sealed record RecordTeamNoShowRequest
{
    [Required(ErrorMessage = "Die nicht angetretene Mannschaft ist erforderlich.")]
    public Guid NoShowTeamId { get; init; }
}