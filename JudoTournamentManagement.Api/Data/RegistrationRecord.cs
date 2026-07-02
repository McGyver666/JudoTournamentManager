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
