using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for updating a tournament.
/// </summary>
public sealed record UpdateTournamentRequest
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

    /// <summary>
    /// Side color used for the non-white athlete in the UI.
    /// </summary>
    [Required]
    [RegularExpression("^(Blue|Red)$", ErrorMessage = "Die Farbseite muss Blue oder Red sein.")]
    public string AccentSideColor { get; init; } = "Blue";
}
