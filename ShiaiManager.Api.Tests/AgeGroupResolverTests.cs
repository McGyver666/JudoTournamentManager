using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;

namespace ShiaiManager.Api.Tests;

public sealed class AgeGroupResolverTests
{
    private const int TournamentYear = 2026;

    private static readonly TournamentCategoryPreset[] DefaultLikePresets =
    [
        Preset("U9", Gender.Male, 8, 6),
        Preset("U11", Gender.Male, 10, 8),
        Preset("U13", Gender.Male, 12, 10),
        Preset("U21", Gender.Male, 20, 17),
        Preset("Männer", Gender.Male, null, 17),
        Preset("U11", Gender.Female, 10, 8),
        Preset("U13", Gender.Female, 12, 10)
    ];

    [Theory]
    [Trait("Category", "UnitTest")]
    [InlineData(2016, "U11")]
    [InlineData(2018, "U9")]
    [InlineData(2008, "U21")]
    [InlineData(1991, "Männer")]
    [InlineData(1950, "Männer")]
    public void GetNaturalAgeGroup_PrefersLowestMinimumAgeThenLowestMaximumAge(int birthYear, string expected)
    {
        Assert.Equal(expected, AgeGroupResolver.GetNaturalAgeGroup(birthYear, Gender.Male, DefaultLikePresets));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void GetNaturalAgeGroup_TreatsMissingMinimumAgeAsLowest()
    {
        TournamentCategoryPreset[] presets = [Preset("U11", Gender.Female, 10, 8), Preset("Offen", Gender.Female, 12, null)];

        Assert.Equal("Offen", AgeGroupResolver.GetNaturalAgeGroup(2016, Gender.Female, presets));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void GetNaturalAgeGroup_WhenNoPresetCoversBirthYear_ReturnsNull()
    {
        Assert.Null(AgeGroupResolver.GetNaturalAgeGroup(2022, Gender.Female, DefaultLikePresets));
    }

    [Theory]
    [Trait("Category", "UnitTest")]
    [InlineData(null, "U11")]
    [InlineData("", "U11")]
    [InlineData(" u13 ", "U13")]
    [InlineData("U9", null)]
    [InlineData("Männer", null)]
    public void GetEffectiveAgeGroup_UsesCoveringStartAgeGroupOrNaturalAgeGroup(string? startAgeGroup, string? expected)
    {
        Assert.Equal(expected, AgeGroupResolver.GetEffectiveAgeGroup(2016, Gender.Male, startAgeGroup, DefaultLikePresets));
    }

    [Theory]
    [Trait("Category", "UnitTest")]
    [InlineData(null, Gender.Female, true)]
    [InlineData("U13", Gender.Female, true)]
    [InlineData("U21", Gender.Male, false)]
    [InlineData("U9", Gender.Female, false)]
    public void IsAllowedStartAgeGroup_RequiresMatchingGenderAndCoveredBirthYear(
        string? startAgeGroup,
        Gender gender,
        bool expected)
    {
        Assert.Equal(expected, AgeGroupResolver.IsAllowedStartAgeGroup(startAgeGroup, 2016, gender, DefaultLikePresets));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void PresetWarnings_ReportMissingAgeGroupsHiddenPresetsAndOrphanCategories()
    {
        TournamentCategoryPreset[] presets =
        [
            Preset("U11", Gender.Female, 10, 8),
            // Fully covered by U11 and never the natural age group.
            Preset("Kinder", Gender.Female, 10, 9)
        ];
        RegistrationDetail[] registrations =
        [
            Registration(2016, Gender.Female),
            Registration(1990, Gender.Female)
        ];
        Category[] categories =
        [
            Category("U11", Gender.Female),
            Category("U15", Gender.Female),
            Category("U11", Gender.Male)
        ];

        var warnings = CategoryPresetWarnings.Build(presets, registrations, categories, TournamentYear);

        Assert.Contains(new CategoryPresetWarning("presets.warningNoAgeGroup", null, 1), warnings);
        Assert.Contains(new CategoryPresetWarning("presets.warningHiddenAgeGroup", "Kinder", null), warnings);
        Assert.Contains(new CategoryPresetWarning("presets.warningOrphanCategory", "U15", null), warnings);
        Assert.Contains(new CategoryPresetWarning("presets.warningOrphanCategory", "U11", null), warnings);
        Assert.DoesNotContain(new CategoryPresetWarning("presets.warningHiddenAgeGroup", "U11", null), warnings);
    }

    private static TournamentCategoryPreset Preset(string ageGroup, Gender gender, int? maxAgeYears, int? minAgeYears) =>
        new(
            Guid.NewGuid(),
            Guid.Empty,
            ageGroup,
            gender,
            maxAgeYears,
            minAgeYears,
            TournamentYear - maxAgeYears,
            TournamentYear - minAgeYears,
            120,
            [],
            0);

    private static RegistrationDetail Registration(int birthYear, Gender gender) =>
        new(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "Test", "Athlete", birthYear, gender, "Club",
            null, null, null, null, null, 30m, true, DateTimeOffset.UtcNow);

    private static Category Category(string ageGroup, Gender gender) =>
        new(Guid.NewGuid(), Guid.Empty, $"{ageGroup} {gender}", ageGroup, gender, null, null, null, null, 120,
            false, 180, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
