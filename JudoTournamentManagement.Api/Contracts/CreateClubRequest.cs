using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for creating a club.
/// </summary>
public sealed record CreateClubRequest
{
    /// <summary>
    /// Club display name; must be unique within the tournament.
    /// </summary>
    [Required(ErrorMessage = "Der Vereinsname ist erforderlich.")]
    [MaxLength(120, ErrorMessage = "Der Vereinsname darf maximal 120 Zeichen lang sein.")]
    public string Name { get; init; } = string.Empty;
}
