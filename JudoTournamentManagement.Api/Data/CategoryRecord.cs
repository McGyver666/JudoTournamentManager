using System.Text.Json.Serialization;

namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistence model for a competition category within a tournament.
/// </summary>
public sealed class CategoryRecord
{
    /// <summary>Unique category identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tournament.</summary>
    public Guid TournamentId { get; set; }

    /// <summary>User-friendly display name, e.g. "U18 Männer -73 kg".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Age group code, e.g. "U18" or "Senioren".</summary>
    public string AgeGroup { get; set; } = string.Empty;

    /// <summary>Gender stored as the enum member name string.</summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>Upper weight limit in kilograms; null means open weight.</summary>
    public decimal? WeightClassKg { get; set; }

    /// <summary>Minimum birth year (inclusive) for age-based auto-assignment; null means no lower bound.</summary>
    public int? MinBirthYear { get; set; }

    /// <summary>Maximum birth year (inclusive) for age-based auto-assignment; null means no upper bound.</summary>
    public int? MaxBirthYear { get; set; }

    /// <summary>Optional free-text ruleset notes or flags.</summary>
    public string? RulesetNotes { get; set; }

    /// <summary>Match duration in seconds (default 300 = 5 minutes).</summary>
    public int MatchDurationSeconds { get; set; } = 300;

    /// <summary>Whether a golden score phase is enabled after regular time expires.</summary>
    public bool GoldenScoreEnabled { get; set; }

    /// <summary>Golden score duration in seconds (default 180 = 3 minutes).</summary>
    public int GoldenScoreDurationSeconds { get; set; } = 180;

    /// <summary>True once the first real fight has started; blocks further changes.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Bracket format used when the draw was generated; null before any draw is generated. Stored as enum member name string.</summary>
    public string? DrawFormat { get; set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Navigation property to the owning tournament.</summary>
    [JsonIgnore]
    public TournamentRecord? Tournament { get; set; }
}
