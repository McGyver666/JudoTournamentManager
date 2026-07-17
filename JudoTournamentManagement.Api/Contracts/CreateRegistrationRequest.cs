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
    /// Whether the athlete's license was confirmed/verified at registration.
    /// Derived from DokuMe QR code validation result on the server side.
    /// </summary>
    public bool LicenseConfirmed { get; init; }

    /// <summary>
    /// Optional DokuMe QR code URL for license verification.
    /// Must be from https://qr.dokume.net with document type d=l.
    /// If provided, the server will parse and validate the license asynchronously.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Die QR-URL darf maximal 2000 Zeichen lang sein.")]
    public string? DokumeQrUrl { get; init; }

    /// <summary>
    /// If the DokuMe license check fails but the operator overrides it,
    /// this field must contain a brief reason (max 200 characters).
    /// Null or empty indicates no override; a failed check blocks registration unless this is provided.
    /// </summary>
    [StringLength(200, ErrorMessage = "Die Begr\u00fcndung darf maximal 200 Zeichen lang sein.")]
    public string? LicenseCheckOverrideReason { get; init; }
}
