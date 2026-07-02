using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for creating a tournament.
/// </summary>
public sealed record CreateTournamentRequest
{
    /// <summary>
    /// Tournament display name.
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Tournament date.
    /// </summary>
    [Required]
    public DateOnly? Date { get; init; }

    /// <summary>
    /// Venue name or address.
    /// </summary>
    [Required]
    [MaxLength(160)]
    public string Venue { get; init; } = string.Empty;

    /// <summary>
    /// Organizer name.
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string Organizer { get; init; } = string.Empty;
}
