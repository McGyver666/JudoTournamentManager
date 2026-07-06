using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

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
        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registrations);

        var service = new CategoryGenerationService(
            categoriesStore.Object,
            registrationsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var request = new GenerateCategoriesRequest
        {
            GenderMode = CategoryGenerationGenderMode.Female,
            WeightMode = CategoryGenerationWeightMode.StandardClasses,
            MinBirthYear = 2014,
            MaxBirthYear = 2016,
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
    public async Task ApplyAsync_GenerationIsAdditive_NeverDeletesExistingCategories()
    {
        var tournamentId = Guid.NewGuid();

        var categoriesStore = new Mock<ICategoriesStore>(MockBehavior.Strict);
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

        var service = new CategoryGenerationService(
            categoriesStore.Object,
            registrationsStore.Object,
            NullLogger<CategoryGenerationService>.Instance);

        var request = new GenerateCategoriesRequest
        {
            GenderMode = CategoryGenerationGenderMode.Female,
            WeightMode = CategoryGenerationWeightMode.StandardClasses,
            MinBirthYear = 2014,
            MaxBirthYear = 2016,
            MatchDurationSeconds = 180,
            GoldenScoreEnabled = false,
            GoldenScoreDurationSeconds = 180,
        };

        var result = await service.ApplyAsync(tournamentId, request, CancellationToken.None);

        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(0, result.SkippedLockedCount);
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
}
