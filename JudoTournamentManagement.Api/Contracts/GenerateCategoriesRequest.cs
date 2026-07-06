using System.ComponentModel.DataAnnotations;
using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Gender mode used by category generation assistant.
/// </summary>
public enum CategoryGenerationGenderMode
{
    Male,
    Female,
    Mixed
}

/// <summary>
/// Weight-class generation strategy.
/// </summary>
public enum CategoryGenerationWeightMode
{
    StandardClasses,
    AthletesByTargetSize
}

/// <summary>
/// Per-group settings for athlete-driven generation.
/// </summary>
public sealed record CategoryGenerationGroupSetting
{
    /// <summary>
    /// Age group label (e.g. U13, U18, Senioren).
    /// </summary>
    [Required(ErrorMessage = "Die Altersklasse ist erforderlich.")]
    [MaxLength(40, ErrorMessage = "Die Altersklasse darf maximal 40 Zeichen lang sein.")]
    public string AgeGroup { get; init; } = string.Empty;

    /// <summary>
    /// Gender bucket this setting applies to.
    /// </summary>
    [Required(ErrorMessage = "Das Geschlecht ist erforderlich.")]
    public CategoryGenerationGenderMode? GenderMode { get; init; }

    /// <summary>
    /// Target number of athletes per generated class.
    /// </summary>
    [Range(2, 64, ErrorMessage = "Die Zielanzahl muss zwischen 2 und 64 liegen.")]
    public int TargetAthletesPerCategory { get; init; } = 8;

    /// <summary>
    /// Maximum allowed weight gap between adjacent athletes in one class.
    /// </summary>
    [Range(0.1, 50, ErrorMessage = "Die maximale Gewichtsabweichung muss zwischen 0,1 und 50 kg liegen.")]
    public decimal MaxWeightDeviationKg { get; init; } = 2m;
}

/// <summary>
/// Request payload for assisted category generation.
/// </summary>
public sealed record GenerateCategoriesRequest
{
    /// <summary>
    /// Optional lower birth-year bound (inclusive).
    /// </summary>
    [Range(1900, 2100, ErrorMessage = "Das Mindest-Geburtsjahr muss zwischen 1900 und 2100 liegen.")]
    public int? MinBirthYear { get; init; }

    /// <summary>
    /// Optional upper birth-year bound (inclusive).
    /// </summary>
    [Range(1900, 2100, ErrorMessage = "Das Höchst-Geburtsjahr muss zwischen 1900 und 2100 liegen.")]
    public int? MaxBirthYear { get; init; }

    /// <summary>
    /// Gender scope used for generation.
    /// </summary>
    [Required(ErrorMessage = "Der Geschlechtsmodus ist erforderlich.")]
    public CategoryGenerationGenderMode? GenderMode { get; init; }

    /// <summary>
    /// Match duration in seconds to apply to generated categories.
    /// </summary>
    [Range(60, 3600, ErrorMessage = "Die Kampfdauer muss zwischen 60 und 3600 Sekunden liegen.")]
    public int MatchDurationSeconds { get; init; } = 240;

    /// <summary>
    /// Whether generated categories use golden score.
    /// </summary>
    public bool GoldenScoreEnabled { get; init; }

    /// <summary>
    /// Golden score duration in seconds.
    /// </summary>
    [Range(30, 3600, ErrorMessage = "Die Golden-Score-Dauer muss zwischen 30 und 3600 Sekunden liegen.")]
    public int GoldenScoreDurationSeconds { get; init; } = 180;

    /// <summary>
    /// Strategy used to derive weight classes.
    /// </summary>
    [Required(ErrorMessage = "Die Gewichtsklassen-Strategie ist erforderlich.")]
    public CategoryGenerationWeightMode? WeightMode { get; init; }

    /// <summary>
    /// Optional per-group settings for athlete-driven mode.
    /// </summary>
    public IReadOnlyList<CategoryGenerationGroupSetting> GroupSettings { get; init; } = [];

}

/// <summary>
/// One generated category proposal.
/// </summary>
public sealed record GeneratedCategoryProposal(
    string Name,
    string AgeGroup,
    Gender Gender,
    decimal? WeightClassKg,
    int? MinBirthYear,
    int? MaxBirthYear,
    int MatchDurationSeconds,
    bool GoldenScoreEnabled,
    int GoldenScoreDurationSeconds,
    int EstimatedAthleteCount,
    string Source);

/// <summary>
/// Preview response for generation assistant.
/// </summary>
public sealed record CategoryGenerationPreviewResponse(
    int ProposedCount,
    IReadOnlyList<GeneratedCategoryProposal> Categories,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Apply response for generation assistant.
/// </summary>
public sealed record CategoryGenerationApplyResponse(
    int CreatedCount,
    int DeletedCount,
    int SkippedDuplicateCount,
    int SkippedLockedCount,
    IReadOnlyList<Category> CreatedCategories,
    IReadOnlyList<string> Warnings);
