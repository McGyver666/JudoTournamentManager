using System.Text.Json.Serialization;

namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistence model for an athlete's registration in a category.
/// An athlete may hold exactly one registration per tournament.
/// </summary>
public sealed class RegistrationRecord
{
    /// <summary>Unique registration identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tournament this registration belongs to (denormalized for efficient queries).</summary>
    public Guid TournamentId { get; set; }

    /// <summary>Registered athlete.</summary>
    public Guid AthleteId { get; set; }

    /// <summary>Target category (null until assigned during weight-in or later).</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Whether the athlete's license was confirmed/verified at registration.</summary>
    public bool LicenseConfirmed { get; set; }

    /// <summary>License number captured from the DokuMe pass (NO claim). Nullable for backward compatibility.</summary>
    public string? LicenseNumber { get; set; }

    /// <summary>Pass expiry date from DokuMe QR code (exp claim, UTC). Nullable for backward compatibility.</summary>
    public DateOnly? PassExpiryDate { get; set; }

    /// <summary>Timestamp when the license was checked/verified. Null if not yet checked or only manually confirmed.</summary>
    public DateTimeOffset? LicenseVerifiedAtUtc { get; set; }

    /// <summary>Username of the operator who performed the license check or override. Null if not checked.</summary>
    public string? LicenseVerifiedByUser { get; set; }

    /// <summary>Whether the license check passed all validation rules (name, birth year, expiry date).</summary>
    public bool? LicenseCheckPassed { get; set; }

    /// <summary>If LicenseCheckPassed is false but LicenseConfirmed is true, this contains the operator's override reason.</summary>
    public string? LicenseOverrideReason { get; set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Navigation property to the owning tournament.</summary>
    [JsonIgnore]
    public TournamentRecord? Tournament { get; set; }

    /// <summary>Navigation property to the athlete.</summary>
    [JsonIgnore]
    public AthleteRecord? Athlete { get; set; }

    /// <summary>Navigation property to the category.</summary>
    [JsonIgnore]
    public CategoryRecord? Category { get; set; }
}
