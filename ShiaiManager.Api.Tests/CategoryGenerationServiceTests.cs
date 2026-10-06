using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ShiaiManager.Api.Tests;

public sealed class CategoryGenerationServiceTests
{
    private static readonly Guid TournamentId = Guid.NewGuid();

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_StandardClasses_UsesIntervalBasedEstimatedCounts()
    {
        var service = CreateService(
            [
                Registration(Gender.Female, 2015, 29m),
                Registration(Gender.Female, 2015, 30m),
                Registration(Gender.Female, 2015, 31m),
                Registration(Gender.Female, 2015, 40m),
                Registration(Gender.Female, 2015, 58m),
                Registration(Gender.Male, 2015, 31m),
            ],
            [Preset("U13", Gender.Female, 12, 10, 2014, 2016, 1) with
            {
                WeightClassLimitsKg = [30m, 33m, 36m, 40m, 44m, 48m, 52m, 57m, null]
            }]);

        var preview = await service.Object.PreviewAsync(TournamentId, Request("U13", CategoryGenerationGenderMode.Female), CancellationToken.None);

        Assert.Equal(2, preview.Categories.Single(x => x.WeightClassKg == 30m).EstimatedAthleteCount);
        Assert.Equal(1, preview.Categories.Single(x => x.WeightClassKg == 33m).EstimatedAthleteCount);
        Assert.Equal(1, preview.Categories.Single(x => x.WeightClassKg is null).EstimatedAthleteCount);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_OverlappingPresets_UsesNaturalAgeGroupInsteadOfPresetOrder()
    {
        var service = CreateService(
            [Registration(Gender.Female, 2016, 29m)],
            [
                Preset("U13", Gender.Female, 12, 10, 2014, 2016, 0),
                Preset("U11", Gender.Female, 10, 8, 2016, 2018, 1)
            ]);

        var preview = await service.Object.PreviewAsync(
            TournamentId,
            Request("U11", CategoryGenerationGenderMode.Female, CategoryGenerationWeightMode.AthletesByTargetSize),
            CancellationToken.None);

        Assert.Equal("U11", Assert.Single(preview.Categories).AgeGroup);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_DefaultAdultPreset_IncludesAthletesOlderThan25()
    {
        // Default "Männer" preset: at least 17 years, no upper age limit (tournament year 2026).
        var service = CreateService(
            [Registration(Gender.Male, 1991, 70m), Registration(Gender.Male, 2008, 66m)],
            [
                Preset("U21", Gender.Male, 20, 17, 2006, 2009, 0),
                new TournamentCategoryPreset(
                    Guid.NewGuid(), TournamentId, "Männer", Gender.Male, null, 17,
                    null, 2009, 240, [66m, 73m, null], 1)
            ]);

        var preview = await service.Object.PreviewAsync(
            TournamentId,
            Request("Männer", CategoryGenerationGenderMode.Male),
            CancellationToken.None);

        var affected = Assert.Single(preview.AffectedRegistrations);
        Assert.Equal(1991, affected.BirthYear);
        Assert.All(preview.Categories, category => Assert.Null(category.MinBirthYear));
        Assert.Equal(1, preview.Categories.Single(x => x.WeightClassKg == 73m).EstimatedAthleteCount);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_SingleOpenPreset_GroupsAllChildrenByWeight()
    {
        var openPreset = new TournamentCategoryPreset(
            Guid.NewGuid(), TournamentId, "Offen", Gender.Female, 12, 6, 2014, 2020, 120, [], 0);
        var service = CreateService(
            [
                Registration(Gender.Female, 2019, 20m),
                Registration(Gender.Female, 2016, 21m),
                Registration(Gender.Female, 2014, 22m)
            ],
            [openPreset]);

        var preview = await service.Object.PreviewAsync(
            TournamentId,
            Request("Offen", CategoryGenerationGenderMode.Female, CategoryGenerationWeightMode.AthletesByTargetSize),
            CancellationToken.None);

        Assert.Equal(3, preview.AffectedRegistrations.Count);
        var category = Assert.Single(preview.Categories);
        Assert.Equal(3, category.EstimatedAthleteCount);
        Assert.Equal(2014, category.MinBirthYear);
        Assert.Equal(2020, category.MaxBirthYear);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_SelectedAgeGroup_IncludesHigherStartersAndUsesPresetBirthYears()
    {
        var service = CreateService(
            [Registration(Gender.Female, 2016, 29m) with { StartAgeGroup = "U13" }],
            [
                Preset("U11", Gender.Female, 10, 8, 2016, 2018, 0),
                Preset("U13", Gender.Female, 12, 10, 2014, 2016, 1)
            ]);

        var preview = await service.Object.PreviewAsync(
            TournamentId,
            Request("U13", CategoryGenerationGenderMode.Female, CategoryGenerationWeightMode.AthletesByTargetSize),
            CancellationToken.None);

        var proposal = Assert.Single(preview.Categories);
        Assert.Equal("U13", proposal.AgeGroup);
        Assert.Equal(2014, proposal.MinBirthYear);
        Assert.Equal(2016, proposal.MaxBirthYear);
        Assert.Equal(1, proposal.EstimatedAthleteCount);
        var affected = Assert.Single(preview.AffectedRegistrations);
        Assert.Equal("U13", affected.StartAgeGroup);
        Assert.Equal(2016, affected.BirthYear);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_NaturalRunExcludesHigherStarter()
    {
        var service = CreateService(
            [Registration(Gender.Female, 2016, 29m) with { StartAgeGroup = "U13" }],
            [
                Preset("U11", Gender.Female, 10, 8, 2016, 2018, 0),
                Preset("U13", Gender.Female, 12, 10, 2014, 2016, 1)
            ]);

        var preview = await service.Object.PreviewAsync(
            TournamentId,
            Request("U11", CategoryGenerationGenderMode.Female),
            CancellationToken.None);

        Assert.Empty(preview.AffectedRegistrations);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_StandardClasses_ListsReplacementsOfSelectedAgeGroupOnly()
    {
        var existingCategory = Category("U13", "U13 W -40 kg");
        var service = CreateService(
            [Registration(Gender.Female, 2016, 29m) with { StartAgeGroup = "U13", AthleteWeightKg = null }],
            [
                Preset("U11", Gender.Female, 10, 8, 2016, 2018, 0),
                Preset("U13", Gender.Female, 12, 10, 2014, 2016, 1)
            ],
            [existingCategory, Category("U11", "U11 W -40 kg")]);

        var preview = await service.Object.PreviewAsync(TournamentId, Request("U13", CategoryGenerationGenderMode.Female), CancellationToken.None);

        Assert.NotEmpty(preview.Categories);
        Assert.All(preview.Categories, category => Assert.Equal("U13", category.AgeGroup));
        Assert.Single(preview.AffectedRegistrations);
        Assert.Contains(preview.Warnings, warning => warning.Key == "categories.warningStandardClassesWithoutWeight");
        Assert.Equal(existingCategory.Id, Assert.Single(preview.CategoriesToReplace).Id);
        Assert.True(preview.CanApply);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_WhenCategoryToReplaceHasFights_CannotApply()
    {
        var foughtCategory = Category("U13", "U13 W -40 kg");
        var service = CreateService(
            [],
            [Preset("U13", Gender.Female, 12, 10, 2014, 2016, 0)],
            [foughtCategory],
            categoryIdsWithFights: new HashSet<Guid> { foughtCategory.Id });

        var preview = await service.Object.PreviewAsync(TournamentId, Request("U13", CategoryGenerationGenderMode.Female), CancellationToken.None);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Warnings, warning => warning.Key == "categories.warningCategoriesLocked");
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_AthleteGrouping_UsesOneTargetSizeForTheSelectedAgeGroup()
    {
        var service = CreateService(
            [
                Registration(Gender.Female, 2016, 30m),
                Registration(Gender.Female, 2016, 31m),
                Registration(Gender.Female, 2016, 32m),
                Registration(Gender.Female, 2016, 33m)
            ],
            [Preset("U11", Gender.Female, 10, 8, 2016, 2018, 0)]);

        var preview = await service.Object.PreviewAsync(
            TournamentId,
            Request("U11", CategoryGenerationGenderMode.Female, CategoryGenerationWeightMode.AthletesByTargetSize) with
            {
                TargetAthletesPerCategory = 2,
                MaxWeightDeviationKg = 2m
            },
            CancellationToken.None);

        Assert.Equal(2, preview.ProposedCount);
        Assert.All(preview.Categories, category => Assert.Equal("U11", category.AgeGroup));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_MixedStandardClasses_RejectsPresetWithoutWeightLimits()
    {
        var service = CreateService(
            [],
            [
                Preset("U9", Gender.Male, 8, 6, 2018, 2020, 0) with { WeightClassLimitsKg = [] },
                Preset("U9", Gender.Female, 8, 6, 2018, 2020, 1)
            ]);

        var exception = await Assert.ThrowsAsync<LocalizedOperationException>(() => service.Object.PreviewAsync(
            TournamentId,
            Request("U9", CategoryGenerationGenderMode.Mixed),
            CancellationToken.None));
        Assert.Equal(AgeGroupMessages.PresetWithoutStandardClasses, exception.Localized);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task ApplyAsync_ReplacesOnlyCategoriesOfSelectedAgeGroupAndGender()
    {
        var selectedCategory = Category("U13", "U13 W -40 kg");
        var otherAgeGroup = Category("U11", "U11 W -40 kg");
        var otherGender = Category("U13", "U13 M -40 kg") with { Gender = Gender.Male };
        var service = CreateService(
            [],
            [Preset("U13", Gender.Female, 12, 10, 2014, 2016, 0)],
            [selectedCategory, otherAgeGroup, otherGender]);
        IReadOnlyCollection<Guid>? deletedIds = null;
        IReadOnlyList<NewCategory>? createdCategories = null;
        service.Categories
            .Setup(x => x.ReplaceAsync(
                TournamentId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<IReadOnlyList<NewCategory>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<Guid>, IReadOnlyList<NewCategory>, CancellationToken>(
                (_, ids, categories, _) =>
                {
                    deletedIds = ids;
                    createdCategories = categories;
                })
            .ReturnsAsync(new CategoryReplaceResult(1, [], 0));

        var result = await service.Object.ApplyAsync(TournamentId, Request("U13", CategoryGenerationGenderMode.Female), CancellationToken.None);

        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(selectedCategory.Id, Assert.Single(deletedIds!));
        Assert.Equal(2, createdCategories!.Count);
        Assert.All(createdCategories, category =>
        {
            Assert.Equal("U13", category.AgeGroup);
            Assert.Equal(Gender.Female, category.Gender);
            Assert.StartsWith("[AUTO_GENERATED]", category.RulesetNotes);
        });
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task ApplyAsync_WhenSelectedAgeGroupHasDrawnCategory_AbortsBeforeReplacing()
    {
        var drawnCategory = Category("U13", "U13 W -40") with { DrawFormat = BracketFormat.RoundRobin };
        var service = CreateService(
            [],
            [Preset("U13", Gender.Female, 12, 10, 2014, 2016, 0)],
            [drawnCategory]);

        var exception = await Assert.ThrowsAsync<LocalizedOperationException>(() =>
            service.Object.ApplyAsync(TournamentId, Request("U13", CategoryGenerationGenderMode.Female), CancellationToken.None));

        Assert.Equal(AgeGroupMessages.CategoriesCannotBeReplaced, exception.Localized);
        service.Categories.Verify(
            x => x.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IReadOnlyList<NewCategory>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task ApplyAsync_WhenStoreRejectsReplacement_Throws()
    {
        var service = CreateService(
            [],
            [Preset("U13", Gender.Female, 12, 10, 2014, 2016, 0)],
            [Category("U13", "U13 W -40")]);
        service.Categories
            .Setup(x => x.ReplaceAsync(
                TournamentId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<IReadOnlyList<NewCategory>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CategoryReplaceResult?)null);

        var exception = await Assert.ThrowsAsync<LocalizedOperationException>(() =>
            service.Object.ApplyAsync(TournamentId, Request("U13", CategoryGenerationGenderMode.Female), CancellationToken.None));

        Assert.Equal(AgeGroupMessages.CategoriesCannotBeReplaced, exception.Localized);
    }

    private static ServiceUnderTest CreateService(
        IReadOnlyList<RegistrationDetail> registrations,
        IReadOnlyList<TournamentCategoryPreset> presets,
        IReadOnlyList<Category>? categories = null,
        IReadOnlySet<Guid>? categoryIdsWithFights = null)
    {
        var categoriesStore = new Mock<ICategoriesStore>(MockBehavior.Strict);
        categoriesStore
            .Setup(x => x.GetAllAsync(TournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories ?? []);
        categoriesStore
            .Setup(x => x.GetIdsWithFightsAsync(TournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categoryIdsWithFights ?? new HashSet<Guid>());

        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(TournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registrations);

        var presetsStore = new Mock<ICategoryPresetsStore>(MockBehavior.Strict);
        presetsStore
            .Setup(x => x.GetAllAsync(TournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(presets);

        var service = new CategoryGenerationService(
            categoriesStore.Object,
            registrationsStore.Object,
            presetsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);
        return new ServiceUnderTest(service, categoriesStore);
    }

    private static GenerateCategoriesRequest Request(
        string ageGroup,
        CategoryGenerationGenderMode genderMode,
        CategoryGenerationWeightMode weightMode = CategoryGenerationWeightMode.StandardClasses) =>
        new()
        {
            AgeGroup = ageGroup,
            GenderMode = genderMode,
            WeightMode = weightMode,
            MatchDurationSeconds = 180,
            GoldenScoreDurationSeconds = 180
        };

    private static RegistrationDetail Registration(Gender gender, int birthYear, decimal weightKg) =>
        new(
            Guid.NewGuid(),
            TournamentId,
            Guid.NewGuid(),
            "Test",
            "Athlete",
            birthYear,
            gender,
            "Club",
            null,
            null,
            null,
            null,
            null,
            weightKg,
            true,
            DateTimeOffset.UtcNow);

    private static TournamentCategoryPreset Preset(
        string ageGroup,
        Gender gender,
        int maxAgeYears,
        int minAgeYears,
        int minBirthYear,
        int maxBirthYear,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            TournamentId,
            ageGroup,
            gender,
            maxAgeYears,
            minAgeYears,
            minBirthYear,
            maxBirthYear,
            180,
            [40m, null],
            sortOrder);

    private static Category Category(string ageGroup, string name) => new(
        Guid.NewGuid(),
        TournamentId,
        name,
        ageGroup,
        Gender.Female,
        40m,
        2014,
        2016,
        null,
        180,
        false,
        180,
        null,
        false,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);

    private sealed record ServiceUnderTest(CategoryGenerationService Object, Mock<ICategoriesStore> Categories);
}
