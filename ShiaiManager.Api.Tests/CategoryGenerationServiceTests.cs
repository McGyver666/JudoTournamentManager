using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ShiaiManager.Api.Tests;

public sealed class CategoryGenerationServiceTests
{
    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_StandardClasses_UsesIntervalBasedEstimatedCounts()
    {
        var tournamentId = Guid.NewGuid();

        var registrations = new List<RegistrationDetail>
        {
            Registration(Guid.NewGuid(), Gender.Female, 2015, 29m),
            Registration(Guid.NewGuid(), Gender.Female, 2015, 30m),
            Registration(Guid.NewGuid(), Gender.Female, 2015, 31m),
            Registration(Guid.NewGuid(), Gender.Female, 2015, 40m),
            Registration(Guid.NewGuid(), Gender.Female, 2015, 58m),
            Registration(Guid.NewGuid(), Gender.Male, 2015, 31m),
        };

        var categoriesStore = new Mock<ICategoriesStore>(MockBehavior.Strict);
        categoriesStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registrations);

        var mockPresets = new Mock<ICategoryPresetsStore>();
        mockPresets
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentCategoryPreset>
            {
                new(
                    Guid.NewGuid(),
                    tournamentId,
                    "U13",
                    Gender.Female,
                    12,
                    10,
                    2014,
                    2016,
                    180,
                    new decimal?[] { 30m, 33m, 36m, 40m, 44m, 48m, 52m, 57m, null },
                    1)
            });
        var service = new CategoryGenerationService(
            categoriesStore.Object,
            registrationsStore.Object,
            mockPresets.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var request = new GenerateCategoriesRequest
        {
            AgeGroup = "U13",
            GenderMode = CategoryGenerationGenderMode.Female,
            WeightMode = CategoryGenerationWeightMode.StandardClasses,
            MatchDurationSeconds = 180,
            GoldenScoreEnabled = false,
            GoldenScoreDurationSeconds = 180,
        };

        var preview = await service.PreviewAsync(tournamentId, request, CancellationToken.None);

        var minus30 = preview.Categories.Single(x => x.AgeGroup == "U13" && x.WeightClassKg == 30m);
        var minus33 = preview.Categories.Single(x => x.AgeGroup == "U13" && x.WeightClassKg == 33m);
        var plus57 = preview.Categories.Single(x => x.AgeGroup == "U13" && x.WeightClassKg is null);

        Assert.Equal(2, minus30.EstimatedAthleteCount);
        Assert.Equal(1, minus33.EstimatedAthleteCount);
        Assert.Equal(1, plus57.EstimatedAthleteCount);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_OverlappingPresets_UsesNaturalAgeGroupInsteadOfPresetOrder()
    {
        var tournamentId = Guid.NewGuid();
        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Registration(Guid.NewGuid(), Gender.Female, 2016, 29m)]);

        var presetsStore = new Mock<ICategoryPresetsStore>(MockBehavior.Strict);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Preset(tournamentId, "U13", Gender.Female, 12, 10, 2014, 2016, 0),
                Preset(tournamentId, "U11", Gender.Female, 10, 8, 2016, 2018, 1)
            ]);

        var service = new CategoryGenerationService(
            new Mock<ICategoriesStore>().Object,
            registrationsStore.Object,
            presetsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var preview = await service.PreviewAsync(
            tournamentId,
            new GenerateCategoriesRequest
            {
                AgeGroup = "U11",
                GenderMode = CategoryGenerationGenderMode.Female,
                WeightMode = CategoryGenerationWeightMode.AthletesByTargetSize,
                MatchDurationSeconds = 180,
                GoldenScoreDurationSeconds = 180
            },
            CancellationToken.None);

        Assert.Equal("U11", Assert.Single(preview.Categories).AgeGroup);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_StandardClasses_IncludesUnboundedSeniorPreset()
    {
        var tournamentId = Guid.NewGuid();
        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Registration(Guid.NewGuid(), Gender.Male, 1995, 70m)]);

        var presetsStore = new Mock<ICategoryPresetsStore>(MockBehavior.Strict);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TournamentCategoryPreset(
                    Guid.NewGuid(), tournamentId, "Männer", Gender.Male, 17, null,
                    null, null, 240, [66m, 73m, null], 0)
            ]);

        var service = new CategoryGenerationService(
            new Mock<ICategoriesStore>().Object,
            registrationsStore.Object,
            presetsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var preview = await service.PreviewAsync(
            tournamentId,
            new GenerateCategoriesRequest
            {
                AgeGroup = "Männer",
                GenderMode = CategoryGenerationGenderMode.Male,
                WeightMode = CategoryGenerationWeightMode.StandardClasses,
                MatchDurationSeconds = 240,
                GoldenScoreDurationSeconds = 180
            },
            CancellationToken.None);

        Assert.Contains(preview.Categories, x => x.AgeGroup == "Männer");
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_SelectedAgeGroup_IncludesHigherStartersAndUsesPresetBirthYears()
    {
        var tournamentId = Guid.NewGuid();
        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Registration(Guid.NewGuid(), Gender.Female, 2016, 29m) with { StartAgeGroup = "U13" }
            ]);

        var presetsStore = new Mock<ICategoryPresetsStore>(MockBehavior.Strict);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Preset(tournamentId, "U11", Gender.Female, 10, 8, 2016, 2018, 0),
                Preset(tournamentId, "U13", Gender.Female, 12, 10, 2014, 2016, 1)
            ]);

        var service = new CategoryGenerationService(
            new Mock<ICategoriesStore>().Object,
            registrationsStore.Object,
            presetsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var preview = await service.PreviewAsync(
            tournamentId,
            new GenerateCategoriesRequest
            {
                AgeGroup = "U13",
                GenderMode = CategoryGenerationGenderMode.Female,
                WeightMode = CategoryGenerationWeightMode.AthletesByTargetSize,
                MatchDurationSeconds = 180,
                GoldenScoreDurationSeconds = 180
            },
            CancellationToken.None);

        var proposal = Assert.Single(preview.Categories);
        Assert.Equal("U13", proposal.AgeGroup);
        Assert.Equal(2014, proposal.MinBirthYear);
        Assert.Equal(2016, proposal.MaxBirthYear);
        Assert.Equal(1, proposal.EstimatedAthleteCount);
        var affectedAthlete = Assert.Single(preview.AffectedAthletes);
        Assert.Equal("U13", affectedAthlete.StartAgeGroup);
        Assert.Equal(2016, affectedAthlete.BirthYear);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_StandardClasses_UsesOnlySelectedAgeGroup()
    {
        var tournamentId = Guid.NewGuid();
        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Registration(Guid.NewGuid(), Gender.Female, 2016, 29m) with
                {
                    StartAgeGroup = "U13",
                    AthleteWeightKg = null
                }
            ]);

        var presetsStore = new Mock<ICategoryPresetsStore>(MockBehavior.Strict);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Preset(tournamentId, "U11", Gender.Female, 10, 8, 2016, 2018, 0),
                Preset(tournamentId, "U13", Gender.Female, 12, 10, 2014, 2016, 1)
            ]);

        var categoriesStore = new Mock<ICategoriesStore>(MockBehavior.Strict);
        var existingCategory = Category(tournamentId, "U13", "U13 W -40 kg");
        categoriesStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingCategory, Category(tournamentId, "U11", "U11 W -40 kg")]);

        var service = new CategoryGenerationService(
            categoriesStore.Object,
            registrationsStore.Object,
            presetsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var preview = await service.PreviewAsync(
            tournamentId,
            new GenerateCategoriesRequest
            {
                AgeGroup = "U13",
                GenderMode = CategoryGenerationGenderMode.Female,
                WeightMode = CategoryGenerationWeightMode.StandardClasses,
                MatchDurationSeconds = 180,
                GoldenScoreDurationSeconds = 180
            },
            CancellationToken.None);

        Assert.NotEmpty(preview.Categories);
        Assert.All(preview.Categories, category => Assert.Equal("U13", category.AgeGroup));
        Assert.Single(preview.AffectedAthletes);
        Assert.Contains(preview.Warnings, warning => warning.Key == "categories.warningStandardClassesWithoutWeight");
        Assert.Equal(existingCategory.Id, Assert.Single(preview.CategoriesToReplace).Id);
        Assert.True(preview.CanApply);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_AthleteGrouping_UsesOneTargetSizeForTheSelectedAgeGroup()
    {
        var tournamentId = Guid.NewGuid();
        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Registration(Guid.NewGuid(), Gender.Female, 2016, 30m),
                Registration(Guid.NewGuid(), Gender.Female, 2016, 31m),
                Registration(Guid.NewGuid(), Gender.Female, 2016, 32m),
                Registration(Guid.NewGuid(), Gender.Female, 2016, 33m)
            ]);

        var presetsStore = new Mock<ICategoryPresetsStore>(MockBehavior.Strict);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Preset(tournamentId, "U11", Gender.Female, 10, 8, 2016, 2018, 0)]);

        var service = new CategoryGenerationService(
            new Mock<ICategoriesStore>().Object,
            registrationsStore.Object,
            presetsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var preview = await service.PreviewAsync(
            tournamentId,
            new GenerateCategoriesRequest
            {
                AgeGroup = "U11",
                GenderMode = CategoryGenerationGenderMode.Female,
                WeightMode = CategoryGenerationWeightMode.AthletesByTargetSize,
                TargetAthletesPerCategory = 2,
                MaxWeightDeviationKg = 2m,
                MatchDurationSeconds = 120,
                GoldenScoreDurationSeconds = 180
            },
            CancellationToken.None);

        Assert.Equal(2, preview.ProposedCount);
        Assert.All(preview.Categories, category => Assert.Equal("U11", category.AgeGroup));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task PreviewAsync_MixedStandardClasses_RejectsPresetWithoutWeightLimits()
    {
        var tournamentId = Guid.NewGuid();
        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var presetsStore = new Mock<ICategoryPresetsStore>(MockBehavior.Strict);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Preset(tournamentId, "U9", Gender.Male, 8, 6, 2018, 2020, 0) with { WeightClassLimitsKg = [] },
                Preset(tournamentId, "U9", Gender.Female, 8, 6, 2018, 2020, 1)
            ]);

        var service = new CategoryGenerationService(
            new Mock<ICategoriesStore>().Object,
            registrationsStore.Object,
            presetsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync(
            tournamentId,
            new GenerateCategoriesRequest
            {
                AgeGroup = "U9",
                GenderMode = CategoryGenerationGenderMode.Mixed,
                WeightMode = CategoryGenerationWeightMode.StandardClasses,
                MatchDurationSeconds = 120,
                GoldenScoreDurationSeconds = 180
            },
            CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task ApplyAsync_ReplacesOnlyUnlockedCategoriesForSelectedAgeGroup()
    {
        var tournamentId = Guid.NewGuid();
        var existingSelectedCategory = Category(tournamentId, "U13", "U13 W -40 kg");
        var existingOtherCategory = Category(tournamentId, "U11", "U11 W -40 kg");

        var categoriesStore = new Mock<ICategoriesStore>(MockBehavior.Strict);
        categoriesStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingSelectedCategory, existingOtherCategory]);
        categoriesStore
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        categoriesStore
            .Setup(x => x.CreateAsync(
                tournamentId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Gender>(),
                It.IsAny<decimal?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RegistrationDetail>());

        var mockPresets = new Mock<ICategoryPresetsStore>();
        mockPresets
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Preset(tournamentId, "U13", Gender.Female, 12, 10, 2014, 2016, 0)]);
        var service = new CategoryGenerationService(
            categoriesStore.Object,
            registrationsStore.Object,
            mockPresets.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var request = new GenerateCategoriesRequest
        {
            AgeGroup = "U13",
            GenderMode = CategoryGenerationGenderMode.Female,
            WeightMode = CategoryGenerationWeightMode.StandardClasses,
            MatchDurationSeconds = 180,
            GoldenScoreEnabled = false,
            GoldenScoreDurationSeconds = 180,
        };

        var result = await service.ApplyAsync(tournamentId, request, CancellationToken.None);

        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(0, result.SkippedLockedCount);
        categoriesStore.Verify(x => x.DeleteAsync(existingSelectedCategory.Id, It.IsAny<CancellationToken>()), Times.Once);
        categoriesStore.Verify(x => x.DeleteAsync(existingOtherCategory.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task ApplyAsync_WhenSelectedAgeGroupHasDrawnCategory_AbortsBeforeDeleting()
    {
        var tournamentId = Guid.NewGuid();
        var drawnCategory = Category(tournamentId, "U13", "U13 W -40") with
        {
            DrawFormat = BracketFormat.RoundRobin
        };
        var categoriesStore = new Mock<ICategoriesStore>(MockBehavior.Strict);
        categoriesStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([drawnCategory]);
        categoriesStore
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var presetsStore = new Mock<ICategoryPresetsStore>(MockBehavior.Strict);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Preset(tournamentId, "U13", Gender.Female, 12, 10, 2014, 2016, 0)]);

        var service = new CategoryGenerationService(
            categoriesStore.Object,
            registrationsStore.Object,
            presetsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var request = new GenerateCategoriesRequest
        {
            AgeGroup = "U13",
            GenderMode = CategoryGenerationGenderMode.Female,
            WeightMode = CategoryGenerationWeightMode.StandardClasses,
            MatchDurationSeconds = 180,
            GoldenScoreDurationSeconds = 180
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyAsync(tournamentId, request, CancellationToken.None));
        categoriesStore.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RegistrationDetail Registration(Guid athleteId, Gender gender, int birthYear, decimal weightKg)
    {
        return new RegistrationDetail(
            Guid.NewGuid(),
            Guid.NewGuid(),
            athleteId,
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
            null,
            weightKg,
            true,
            DateTimeOffset.UtcNow);
    }

    private static TournamentCategoryPreset Preset(
        Guid tournamentId,
        string ageGroup,
        Gender gender,
        int maxAgeYears,
        int minAgeYears,
        int minBirthYear,
        int maxBirthYear,
        int sortOrder)
    {
        return new TournamentCategoryPreset(
            Guid.NewGuid(),
            tournamentId,
            ageGroup,
            gender,
            maxAgeYears,
            minAgeYears,
            minBirthYear,
            maxBirthYear,
            180,
            [40m, null],
            sortOrder);
    }

    private static Category Category(Guid tournamentId, string ageGroup, string name) => new(
        Guid.NewGuid(),
        tournamentId,
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
}
