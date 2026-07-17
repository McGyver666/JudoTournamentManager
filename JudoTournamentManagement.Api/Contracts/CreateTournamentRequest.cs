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

    /// <summary>
    /// Side color used for the non-white athlete in the UI.
    /// </summary>
    [Required]
    [RegularExpression("^(Blue|Red)$", ErrorMessage = "Die Farbseite muss Blue oder Red sein.")]
    public string AccentSideColor { get; init; } = "Blue";

    /// <summary>Hold duration in seconds for Ippon. Must be between 10 and 60.</summary>
    [Range(10, 60)]
    public int OsaeKomiIpponSeconds { get; init; } = 20;

    /// <summary>Hold duration in seconds for Waza-ari. Must be between 5 and 30.</summary>
    [Range(5, 30)]
    public int OsaeKomiWazaAriSeconds { get; init; } = 10;

    /// <summary>Hold duration in seconds for Yuko. Must be between 1 and 15.</summary>
    [Range(1, 15)]
    public int OsaeKomiYukoSeconds { get; init; } = 5;

    /// <summary>Whether Yuko is awarded for a hold between Yuko and Waza-ari thresholds.</summary>
    public bool OsaeKomiYukoEnabled { get; init; } = true;
}
