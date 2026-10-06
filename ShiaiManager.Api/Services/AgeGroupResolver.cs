using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Resolves natural and explicitly selected tournament preset age groups.
/// </summary>
public static class AgeGroupResolver
{
    /// <summary>
    /// Returns the natural age group for an athlete, or <see langword="null"/> when no preset matches.
    /// </summary>
    public static string? GetNaturalAgeGroup(
        int birthYear,
        Gender gender,
        IReadOnlyList<TournamentCategoryPreset> presets)
    {
        return presets
            .Where(p => p.Gender == gender && CoversBirthYear(p, birthYear))
            .OrderBy(p => p.MinAgeYears ?? int.MaxValue)
            .ThenBy(p => p.MaxAgeYears ?? int.MaxValue)
            .ThenBy(p => p.AgeGroup, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.AgeGroup)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns a valid selected start age group or falls back to the natural age group when none is selected.
    /// </summary>
    public static string? GetEffectiveAgeGroup(
        int birthYear,
        Gender gender,
        string? startAgeGroup,
        IReadOnlyList<TournamentCategoryPreset> presets)
    {
        if (string.IsNullOrWhiteSpace(startAgeGroup))
        {
            return GetNaturalAgeGroup(birthYear, gender, presets);
        }

        return presets
            .Where(p => p.Gender == gender
                && StringComparer.OrdinalIgnoreCase.Equals(p.AgeGroup, startAgeGroup.Trim())
                && CoversBirthYear(p, birthYear))
            .OrderBy(p => p.SortOrder)
            .Select(p => p.AgeGroup)
            .FirstOrDefault();
    }

    /// <summary>
    /// Determines whether a preset covers the athlete's inclusive birth-year range.
    /// </summary>
    public static bool CoversBirthYear(TournamentCategoryPreset preset, int birthYear) =>
        (!preset.MinBirthYear.HasValue || birthYear >= preset.MinBirthYear.Value)
        && (!preset.MaxBirthYear.HasValue || birthYear <= preset.MaxBirthYear.Value);
}