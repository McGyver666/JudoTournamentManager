namespace ShiaiManager.Api.Models;

/// <summary>
/// Values for a category that is about to be created.
/// </summary>
/// <param name="Name">User-friendly display name.</param>
/// <param name="AgeGroup">Age group code.</param>
/// <param name="Gender">Category gender.</param>
/// <param name="WeightClassKg">Upper weight limit in kilograms; null means open weight.</param>
/// <param name="MinBirthYear">Minimum birth year (inclusive); null means no lower bound.</param>
/// <param name="MaxBirthYear">Maximum birth year (inclusive); null means no upper bound.</param>
/// <param name="RulesetNotes">Optional free-text ruleset notes.</param>
/// <param name="MatchDurationSeconds">Match duration in seconds.</param>
/// <param name="GoldenScoreEnabled">Whether golden score is enabled.</param>
/// <param name="GoldenScoreDurationSeconds">Golden score duration in seconds.</param>
public sealed record NewCategory(
    string Name,
    string AgeGroup,
    Gender Gender,
    decimal? WeightClassKg,
    int? MinBirthYear,
    int? MaxBirthYear,
    string? RulesetNotes,
    int MatchDurationSeconds,
    bool GoldenScoreEnabled,
    int GoldenScoreDurationSeconds);

/// <summary>
/// Outcome of atomically replacing categories.
/// </summary>
/// <param name="DeletedCount">Number of deleted categories.</param>
/// <param name="Created">Categories that were created.</param>
/// <param name="SkippedDuplicateCount">New categories skipped because an equal category already existed.</param>
public sealed record CategoryReplaceResult(
    int DeletedCount,
    IReadOnlyList<Category> Created,
    int SkippedDuplicateCount);
