using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Resolves natural and explicitly selected tournament preset age groups.
/// </summary>
public static class AgeGroupResolver
{
    /// <summary>
    /// Returns the natural age group for an athlete, or <see langword="null"/> when no preset matches.
    /// The preset with the lowest minimum age wins (no minimum counts as lowest); ties go to the lower maximum age.
    /// </summary>
    public static string? GetNaturalAgeGroup(
        int birthYear,
        Gender gender,
        IReadOnlyList<TournamentCategoryPreset> presets)
    {
        return presets
            .Where(p => p.Gender == gender && CoversBirthYear(p, birthYear))
            .OrderBy(p => p.MinAgeYears ?? 0)
            .ThenBy(p => p.MaxAgeYears ?? int.MaxValue)
            .ThenBy(p => p.AgeGroup, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.AgeGroup)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns a valid selected start age group or falls back to the natural age group when none is selected.
    /// Returns <see langword="null"/> when the selected start age group does not cover the athlete.
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
                && IsSameAgeGroup(p.AgeGroup, startAgeGroup)
                && CoversBirthYear(p, birthYear))
            .OrderBy(p => p.SortOrder)
            .Select(p => p.AgeGroup)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns the effective age group of a registration.
    /// </summary>
    public static string? GetEffectiveAgeGroup(
        RegistrationDetail registration,
        IReadOnlyList<TournamentCategoryPreset> presets) =>
        GetEffectiveAgeGroup(
            registration.AthleteBirthYear,
            registration.AthleteGender,
            registration.StartAgeGroup,
            presets);

    /// <summary>
    /// Determines whether a start age group may be selected for an athlete; empty means natural age group.
    /// </summary>
    public static bool IsAllowedStartAgeGroup(
        string? startAgeGroup,
        int birthYear,
        Gender gender,
        IReadOnlyList<TournamentCategoryPreset> presets) =>
        string.IsNullOrWhiteSpace(startAgeGroup)
        || GetEffectiveAgeGroup(birthYear, gender, startAgeGroup, presets) is not null;

    /// <summary>
    /// Compares two age group codes ignoring surrounding whitespace and case.
    /// </summary>
    public static bool IsSameAgeGroup(string? left, string? right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left?.Trim(), right?.Trim());

    /// <summary>
    /// Determines whether a preset covers the athlete's inclusive birth-year range.
    /// </summary>
    public static bool CoversBirthYear(TournamentCategoryPreset preset, int birthYear) =>
        (!preset.MinBirthYear.HasValue || birthYear >= preset.MinBirthYear.Value)
        && (!preset.MaxBirthYear.HasValue || birthYear <= preset.MaxBirthYear.Value);
}
