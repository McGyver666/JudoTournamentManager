using System.Text.Json.Serialization;

namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistence model for an athlete registered for a tournament.
/// </summary>
public sealed class AthleteRecord
{
    /// <summary>Unique athlete identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tournament.</summary>
    public Guid TournamentId { get; set; }

    /// <summary>Club the athlete competes for.</summary>
    public Guid ClubId { get; set; }

    /// <summary>Given name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Family name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Year of birth.</summary>
    public int BirthYear { get; set; }

    /// <summary>Gender stored as the enum member name string.</summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>Optional federation license identifier.</summary>
    public string? LicenseId { get; set; }

    /// <summary>Optional athlete body weight in kilograms.</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Belt grade as numeric scale (1=9. Kyu ... 9=1. Kyu, 10=1. Dan ... 14=5. Dan).</summary>
    public int Grade { get; set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Navigation property to the owning tournament.</summary>
    [JsonIgnore]
    public TournamentRecord? Tournament { get; set; }

    /// <summary>Navigation property to the athlete's club.</summary>
    [JsonIgnore]
    public ClubRecord? Club { get; set; }
}
