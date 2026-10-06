using System.ComponentModel.DataAnnotations;
using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Contracts;

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
/// Request payload for assisted category generation.
/// </summary>
public sealed record GenerateCategoriesRequest
{
    /// <summary>
    /// Age group selected for this generation run.
    /// </summary>
    [Required(ErrorMessage = "Die Altersklasse ist erforderlich.")]
    [MaxLength(40, ErrorMessage = "Die Altersklasse darf maximal 40 Zeichen lang sein.")]
    public string AgeGroup { get; init; } = string.Empty;

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
    /// Target number of athletes per generated weight group.
    /// </summary>
    [Range(2, 64, ErrorMessage = "Die Zielanzahl muss zwischen 2 und 64 liegen.")]
    public int TargetAthletesPerCategory { get; init; } = 8;

    /// <summary>
    /// Maximum allowed weight gap between adjacent athletes in a group.
    /// </summary>
    [Range(0.1, 50, ErrorMessage = "Die maximale Gewichtsabweichung muss zwischen 0,1 und 50 kg liegen.")]
    public decimal MaxWeightDeviationKg { get; init; } = 2m;

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
/// One registration affected by a category generation preview.
/// </summary>
/// <param name="RegistrationId">Registration identifier.</param>
/// <param name="FirstName">Athlete's given name.</param>
/// <param name="LastName">Athlete's family name.</param>
/// <param name="BirthYear">Athlete's year of birth.</param>
/// <param name="Gender">Athlete's gender.</param>
/// <param name="WeightKg">Athlete's measured weight, if present.</param>
/// <param name="StartAgeGroup">Selected higher-start age group, or null for natural classification.</param>
public sealed record GeneratedAthletePreview(
    Guid RegistrationId,
    string FirstName,
    string LastName,
    int BirthYear,
    Gender Gender,
    decimal? WeightKg,
    string? StartAgeGroup);

/// <summary>
/// Localizable warning returned by category generation.
/// </summary>
/// <param name="Key">Frontend translation key.</param>
/// <param name="Count">Optional affected-item count.</param>
public sealed record CategoryGenerationWarning(string Key, int? Count = null);

/// <summary>
/// Preview response for generation assistant.
/// </summary>
public sealed record CategoryGenerationPreviewResponse(
    int ProposedCount,
    IReadOnlyList<GeneratedCategoryProposal> Categories,
    IReadOnlyList<CategoryGenerationWarning> Warnings)
{
    /// <summary>Existing categories that will be replaced by applying this preview.</summary>
    public IReadOnlyList<Category> CategoriesToReplace { get; init; } = [];

    /// <summary>Registrations included in the selected age-group run.</summary>
    public IReadOnlyList<GeneratedAthletePreview> AffectedAthletes { get; init; } = [];

    /// <summary>Whether the preview can be applied without changing locked or drawn categories.</summary>
    public bool CanApply { get; init; } = true;
}

/// <summary>
/// Apply response for generation assistant.
/// </summary>
public sealed record CategoryGenerationApplyResponse(
    int CreatedCount,
    int DeletedCount,
    int SkippedDuplicateCount,
    int SkippedLockedCount,
    IReadOnlyList<Category> CreatedCategories,
    IReadOnlyList<CategoryGenerationWarning> Warnings);
