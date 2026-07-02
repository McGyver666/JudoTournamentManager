using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for registering an athlete.
/// Captures weight and license confirmation at registration time.
/// Category assignment happens later (via AssignCategoryAsync endpoint or during draw generation).
/// </summary>
public sealed record CreateRegistrationRequest
{
    /// <summary>
    /// Athlete to register.
    /// </summary>
    [Required(ErrorMessage = "Der Athlet ist erforderlich.")]
    public Guid AthleteId { get; init; }

    /// <summary>
    /// Athlete's exact weight in kg captured at weight-in. Mandatory.
    /// </summary>
    [Required(ErrorMessage = "Das Gewicht ist erforderlich.")]
    [Range(1.0, 300.0, ErrorMessage = "Das Gewicht muss zwischen 1.0 und 300.0 kg liegen.")]
    public decimal WeightKg { get; init; }

    /// <summary>
    /// The athlete's license ID (verified/entered at registration).
    /// </summary>
    [StringLength(40, ErrorMessage = "Die Lizenznummer darf maximal 40 Zeichen lang sein.")]
    public string? LicenseId { get; init; }

    /// <summary>
    /// Whether the athlete's license was confirmed/verified at registration.
    /// </summary>
    public bool LicenseConfirmed { get; init; }
}
