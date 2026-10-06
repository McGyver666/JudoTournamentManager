using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Derives the preset-tab warnings: registrations without age group, hidden presets and orphan categories.
/// </summary>
public static class CategoryPresetWarnings
{
    /// <summary>Oldest athlete age considered when probing open-ended preset ranges.</summary>
    private const int MaxProbedAgeYears = 120;

    /// <summary>
    /// Builds all warnings for the given tournament data.
    /// </summary>
    public static IReadOnlyList<CategoryPresetWarning> Build(
        IReadOnlyList<TournamentCategoryPreset> presets,
        IReadOnlyList<RegistrationDetail> registrations,
        IReadOnlyList<Category> categories,
        int tournamentYear)
    {
        var warnings = new List<CategoryPresetWarning>();

        var registrationsWithoutAgeGroup = registrations.Count(r => AgeGroupResolver.GetEffectiveAgeGroup(r, presets) is null);
        if (registrationsWithoutAgeGroup > 0)
        {
            warnings.Add(new CategoryPresetWarning("presets.warningNoAgeGroup", null, registrationsWithoutAgeGroup));
        }

        warnings.AddRange(presets
            .Where(p => !CanBeNaturalAgeGroup(p, presets, tournamentYear))
            .Select(p => p.AgeGroup)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(ageGroup => new CategoryPresetWarning("presets.warningHiddenAgeGroup", ageGroup, null)));

        warnings.AddRange(categories
            .Where(c => !presets.Any(p => AgeGroupResolver.IsSameAgeGroup(p.AgeGroup, c.AgeGroup) && c.AcceptsGender(p.Gender)))
            .Select(c => c.AgeGroup)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(ageGroup => new CategoryPresetWarning("presets.warningOrphanCategory", ageGroup, null)));

        return warnings;
    }

    private static bool CanBeNaturalAgeGroup(
        TournamentCategoryPreset preset,
        IReadOnlyList<TournamentCategoryPreset> presets,
        int tournamentYear)
    {
        var firstBirthYear = preset.MinBirthYear ?? tournamentYear - MaxProbedAgeYears;
        var lastBirthYear = preset.MaxBirthYear ?? tournamentYear;
        for (var birthYear = firstBirthYear; birthYear <= lastBirthYear; birthYear++)
        {
            if (AgeGroupResolver.IsSameAgeGroup(
                AgeGroupResolver.GetNaturalAgeGroup(birthYear, preset.Gender, presets),
                preset.AgeGroup))
            {
                return true;
            }
        }

        return false;
    }
}
