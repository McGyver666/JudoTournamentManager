namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistence model for a tournament category preset row.
/// </summary>
public sealed class CategoryPresetRecord
{
    /// <summary>Unique preset identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tournament identifier.</summary>
    public Guid TournamentId { get; set; }

    /// <summary>Navigation to the owning tournament.</summary>
    public TournamentRecord Tournament { get; set; } = null!;

    /// <summary>Age-group label, e.g. "U13", "Frauen".</summary>
    public string AgeGroup { get; set; } = string.Empty;

    /// <summary>Gender stored as string enum name.</summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>
    /// Maximum athlete age (inclusive). MinBirthYear = tournamentYear - MaxAgeYears.
    /// Null means no oldest-athlete limit.
    /// </summary>
    public int? MaxAgeYears { get; set; }

    /// <summary>
    /// Minimum athlete age (inclusive). MaxBirthYear = tournamentYear - MinAgeYears.
    /// Null means no youngest-athlete limit.
    /// </summary>
    public int? MinAgeYears { get; set; }

    /// <summary>Default match duration in seconds for this age group.</summary>
    public int DefaultMatchDurationSeconds { get; set; } = 240;

    /// <summary>
    /// JSON-serialized array of weight class upper limits in kg (decimal?[]).
    /// A JSON null element represents the open/heavy class.
    /// Example: [27,30,33,36,40,44,48,52,57,null]
    /// </summary>
    public string WeightClassLimitsJson { get; set; } = "[]";

    /// <summary>Display sort order within the tournament's preset list.</summary>
    public int SortOrder { get; set; }
}
