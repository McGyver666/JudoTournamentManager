using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Controllers;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JudoTournamentManagement.Api.Tests;

/// <summary>
/// Unit tests for all API controller endpoints.
/// Tests the controller logic, validation, and HTTP response contracts using mocked dependencies.
/// </summary>
[Trait("Category", "UnitTest")]
public sealed class ControllerUnitTests
{
    #region Tournament Controller Tests

    [Fact]
    public async Task TournamentsController_GetAllAsync_ReturnsOk()
    {
        var mockStore = new Mock<ITournamentStore>();
        mockStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tournament>());
        var controller = new TournamentsController(mockStore.Object);

        var result = await controller.GetAllAsync(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task TournamentsController_CreateAsync_WithValidData_ReturnsCreated()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
            AccentSideColor = "Red"
        };
        var mockStore = new Mock<ITournamentStore>();
        mockStore.Setup(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        var controller = new TournamentsController(mockStore.Object);
        var request = new CreateTournamentRequest { Name = "Test", Date = new DateOnly(2026, 7, 15), Venue = "Venue", Organizer = "Org", AccentSideColor = "Red" };

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        mockStore.Verify(s => s.CreateAsync("Test", new DateOnly(2026, 7, 15), "Venue", "Org", "Red", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TournamentsController_GetByIdAsync_WithValidId_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var mockStore = new Mock<ITournamentStore>();
        mockStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        var controller = new TournamentsController(mockStore.Object);

        var result = await controller.GetByIdAsync(tournamentId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task TournamentsController_GetByIdAsync_WithInvalidId_ReturnsNotFound()
    {
        var mockStore = new Mock<ITournamentStore>();
        mockStore.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tournament?)null);
        var controller = new TournamentsController(mockStore.Object);

        var result = await controller.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task TournamentsController_UpdateAsync_WithValidData_ReturnsNoContent()
    {
        var tournamentId = Guid.NewGuid();
        var mockStore = new Mock<ITournamentStore>();
        mockStore.Setup(s => s.UpdateAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = new TournamentsController(mockStore.Object);
        var request = new UpdateTournamentRequest { Name = "Updated", Date = new DateOnly(2026, 8, 15), Venue = "Venue", Organizer = "Org", AccentSideColor = "Red" };

        var result = await controller.UpdateAsync(tournamentId, request, CancellationToken.None);

        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        mockStore.Verify(s => s.UpdateAsync(
            tournamentId,
            "Updated",
            new DateOnly(2026, 8, 15),
            "Venue",
            "Org",
            "Red",
            request.OsaeKomiIpponSeconds,
            request.OsaeKomiWazaAriSeconds,
            request.OsaeKomiYukoSeconds,
            request.OsaeKomiYukoEnabled,
            request.MinimumRestBetweenFightsSeconds,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TournamentsController_UpdateAsync_WithInvalidId_ReturnsNotFound()
    {
        var mockStore = new Mock<ITournamentStore>();
        mockStore.Setup(s => s.UpdateAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = new TournamentsController(mockStore.Object);
        var request = new UpdateTournamentRequest { Name = "Updated", Date = new DateOnly(2026, 8, 15), Venue = "Venue", Organizer = "Org", AccentSideColor = "Blue" };

        var result = await controller.UpdateAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TournamentsController_DeleteAsync_WithValidId_ReturnsNoContent()
    {
        var tournamentId = Guid.NewGuid();
        var mockStore = new Mock<ITournamentStore>();
        mockStore.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = new TournamentsController(mockStore.Object);

        var result = await controller.DeleteAsync(tournamentId, CancellationToken.None);

        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
    }

    [Fact]
    public async Task TournamentsController_DeleteAsync_WithInvalidId_ReturnsNotFound()
    {
        var mockStore = new Mock<ITournamentStore>();
        mockStore.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = new TournamentsController(mockStore.Object);

        var result = await controller.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Tatami Controller Tests

    [Fact]
    public async Task TatamisController_GetAllAsync_WithValidTournament_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var mockTatamisStore = new Mock<ITatamisStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockTatamisStore.Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tatami>());
        var controller = new TatamisController(mockTatamisStore.Object, mockTournamentStore.Object);

        var result = await controller.GetAllAsync(tournamentId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task TatamisController_CreateAsync_WithValidData_ReturnsCreated()
    {
        var tournamentId = Guid.NewGuid();
        var tatamiId = Guid.NewGuid();
        var mockTatamisStore = new Mock<ITatamisStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockTatamisStore.Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tatami(tatamiId, tournamentId, "Test", 1, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var controller = new TatamisController(mockTatamisStore.Object, mockTournamentStore.Object);
        var request = new CreateTatamiRequest { Name = "Test", DisplayOrder = 1 };

        var result = await controller.CreateAsync(tournamentId, request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
    }

    #endregion

    #region Category Controller Tests

    [Fact]
    public async Task CategoriesController_GetAllAsync_WithValidTournament_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var mockCategoriesStore = new Mock<ICategoriesStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        var mockCategoryGenerationService = new Mock<ICategoryGenerationService>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockCategoriesStore.Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());
        var controller = new CategoriesController(
            mockCategoriesStore.Object,
            mockTournamentStore.Object,
            mockCategoryGenerationService.Object);

        var result = await controller.GetAllAsync(tournamentId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task CategoriesController_CreateAsync_WithValidData_ReturnsCreated()
    {
        var tournamentId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var mockCategoriesStore = new Mock<ICategoriesStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        var mockCategoryGenerationService = new Mock<ICategoryGenerationService>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockCategoriesStore.Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Gender>(), null, null, null, null, It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(categoryId, tournamentId, "U12", "U12", Gender.Male, null, null, null, null, 300, false, 180, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var controller = new CategoriesController(
            mockCategoriesStore.Object,
            mockTournamentStore.Object,
            mockCategoryGenerationService.Object);
        var request = new CreateCategoryRequest { Name = "U12", AgeGroup = "U12", Gender = Gender.Male };

        var result = await controller.CreateAsync(tournamentId, request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
    }

    [Fact]
    public async Task CategoriesController_PreviewGenerationAsync_WithValidData_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var mockCategoriesStore = new Mock<ICategoriesStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        var mockCategoryGenerationService = new Mock<ICategoryGenerationService>();

        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        mockCategoryGenerationService.Setup(s => s.PreviewAsync(
                tournamentId,
                It.IsAny<GenerateCategoriesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CategoryGenerationPreviewResponse(0, [], []));

        var controller = new CategoriesController(
            mockCategoriesStore.Object,
            mockTournamentStore.Object,
            mockCategoryGenerationService.Object);

        var request = new GenerateCategoriesRequest
        {
            GenderMode = CategoryGenerationGenderMode.Male,
            WeightMode = CategoryGenerationWeightMode.StandardClasses
        };

        var result = await controller.PreviewGenerationAsync(tournamentId, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task CategoriesController_ApplyGenerationAsync_WithValidData_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var mockCategoriesStore = new Mock<ICategoriesStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        var mockCategoryGenerationService = new Mock<ICategoryGenerationService>();

        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        mockCategoryGenerationService.Setup(s => s.ApplyAsync(
                tournamentId,
                It.IsAny<GenerateCategoriesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CategoryGenerationApplyResponse(0, 0, 0, 0, [], []));

        var controller = new CategoriesController(
            mockCategoriesStore.Object,
            mockTournamentStore.Object,
            mockCategoryGenerationService.Object);

        var request = new GenerateCategoriesRequest
        {
            GenderMode = CategoryGenerationGenderMode.Male,
            WeightMode = CategoryGenerationWeightMode.StandardClasses
        };

        var result = await controller.ApplyGenerationAsync(tournamentId, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    #endregion

    #region Club Controller Tests

    [Fact]
    public async Task ClubsController_GetAllAsync_WithValidTournament_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var mockClubsStore = new Mock<IClubsStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockClubsStore.Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Club>());
        var controller = new ClubsController(mockClubsStore.Object, mockTournamentStore.Object);

        var result = await controller.GetAllAsync(tournamentId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task ClubsController_CreateAsync_WithValidData_ReturnsCreated()
    {
        var tournamentId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var mockClubsStore = new Mock<IClubsStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockClubsStore.Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Club(clubId, tournamentId, "Test Club", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var controller = new ClubsController(mockClubsStore.Object, mockTournamentStore.Object);
        var request = new CreateClubRequest { Name = "Test Club" };

        var result = await controller.CreateAsync(tournamentId, request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
    }

    #endregion

    #region Athlete Controller Tests

    [Fact]
    public async Task AthletesController_GetAllAsync_WithValidTournament_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var mockAthletesStore = new Mock<IAthletesStore>();
        var mockClubsStore = new Mock<IClubsStore>();
        var mockDm4Parser = new Mock<IDm4AthleteImportParser>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockAthletesStore.Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Athlete>());
        var controller = new AthletesController(
            mockAthletesStore.Object,
            mockClubsStore.Object,
            mockDm4Parser.Object,
            mockTournamentStore.Object);

        var result = await controller.GetAllAsync(tournamentId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task AthletesController_ImportAsync_WithValidData_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var mockAthletesStore = new Mock<IAthletesStore>();
        var mockClubsStore = new Mock<IClubsStore>();
        var mockDm4Parser = new Mock<IDm4AthleteImportParser>();
        var mockTournamentStore = new Mock<ITournamentStore>();

        mockTournamentStore
            .Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));

        mockClubsStore
            .Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Club(clubId, tournamentId, "Test Club", null, null, null, now, now)]);

        mockAthletesStore
            .Setup(s => s.CreateBulkAsync(
                tournamentId,
                It.IsAny<IReadOnlyList<AthleteImportItem>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Athlete(Guid.NewGuid(), tournamentId, clubId, "Max", "Muster", 2010, Gender.Male, "L1", 30.5m, 3, null, null, now, now)
            ]);

        var controller = new AthletesController(
            mockAthletesStore.Object,
            mockClubsStore.Object,
            mockDm4Parser.Object,
            mockTournamentStore.Object);
        var request = new ImportAthletesRequest
        {
            Athletes = [
                new CreateAthleteRequest
                {
                    ClubId = clubId,
                    FirstName = "Max",
                    LastName = "Muster",
                    BirthYear = 2010,
                    Gender = Gender.Male,
                    LicenseId = "L1",
                    WeightKg = 30.5m,
                    Grade = 3
                }
            ]
        };

        var result = await controller.ImportAsync(tournamentId, request, false, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        mockAthletesStore.Verify(s => s.CreateBulkAsync(
            tournamentId,
            It.Is<IReadOnlyList<AthleteImportItem>>(items => items.Count == 1 && items[0].Grade == 3),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AthletesController_ImportFromDm4Async_WithValidData_ReturnsOkAndCreatesMissingClub()
    {
        var tournamentId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var mockAthletesStore = new Mock<IAthletesStore>();
        var mockClubsStore = new Mock<IClubsStore>();
        var mockDm4Parser = new Mock<IDm4AthleteImportParser>();
        var mockTournamentStore = new Mock<ITournamentStore>();

        mockTournamentStore
            .Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));

        mockClubsStore
            .Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        mockClubsStore
            .Setup(s => s.CreateAsync(tournamentId, "JC Teststadt", "Max Beispiel", "kontakt+test@example.invalid", "015000000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Club(clubId, tournamentId, "JC Teststadt", null, null, null, now, now));

        mockDm4Parser
            .Setup(x => x.Parse(It.IsAny<ReadOnlyMemory<byte>>()))
            .Returns(new Dm4AthleteImportData(
                "JC Teststadt",                "Max Beispiel",
                "kontakt+test@example.invalid",
                "015000000001",                Gender.Male,
                [new Dm4AthleteImportRow("Muster", "Max", 3, 30.5m, 2010)]));

        mockAthletesStore
            .Setup(s => s.CreateBulkAsync(
                tournamentId,
                It.IsAny<IReadOnlyList<AthleteImportItem>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Athlete(Guid.NewGuid(), tournamentId, clubId, "Max", "Muster", 2010, Gender.Male, null, 30.5m, 3, null, null, now, now)
            ]);

        var controller = new AthletesController(
            mockAthletesStore.Object,
            mockClubsStore.Object,
            mockDm4Parser.Object,
            mockTournamentStore.Object);

        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "athletes.dm4");

        var result = await controller.ImportFromDm4Async(tournamentId, file, false, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        mockClubsStore.Verify(s => s.CreateAsync(tournamentId, "JC Teststadt", "Max Beispiel", "kontakt+test@example.invalid", "015000000001", It.IsAny<CancellationToken>()), Times.Once);
        mockAthletesStore.Verify(s => s.CreateBulkAsync(
            tournamentId,
            It.Is<IReadOnlyList<AthleteImportItem>>(items =>
                items.Count == 1
                && items[0].FirstName == "Max"
                && items[0].LastName == "Muster"
                && items[0].ClubId == clubId
                && items[0].Gender == Gender.Male),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AthletesController_ImportFromDm4Async_WithParserError_ReturnsValidationProblem()
    {
        var tournamentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var mockAthletesStore = new Mock<IAthletesStore>();
        var mockClubsStore = new Mock<IClubsStore>();
        var mockDm4Parser = new Mock<IDm4AthleteImportParser>();
        var mockTournamentStore = new Mock<ITournamentStore>();

        mockTournamentStore
            .Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));

        mockDm4Parser
            .Setup(x => x.Parse(It.IsAny<ReadOnlyMemory<byte>>()))
            .Throws(new Dm4ImportParseException("Formatfehler"));

        var controller = new AthletesController(
            mockAthletesStore.Object,
            mockClubsStore.Object,
            mockDm4Parser.Object,
            mockTournamentStore.Object);

        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "athletes.dm4");

        var result = await controller.ImportFromDm4Async(tournamentId, file, false, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains("Formatfehler", controller.ModelState[nameof(file)]!.Errors.Select(x => x.ErrorMessage));
    }

    #endregion

    #region Registration Controller Tests

    [Fact]
    public async Task RegistrationsController_GetAllAsync_WithValidTournament_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var mockRegistrationsStore = new Mock<IRegistrationsStore>();
        var mockAthletesStore = new Mock<IAthletesStore>();
        var mockCategoriesStore = new Mock<ICategoriesStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        var mockBracketService = new Mock<IBracketService>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockRegistrationsStore.Setup(s => s.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RegistrationDetail>());
        var mockDokumePassParser = new Mock<IDokumePassParser>();
        var controller = new RegistrationsController(
            mockRegistrationsStore.Object,
            mockAthletesStore.Object,
            mockCategoriesStore.Object,
            mockTournamentStore.Object,
            mockBracketService.Object,
            mockDokumePassParser.Object,
            NullLogger<RegistrationsController>.Instance);

        var result = await controller.GetAllAsync(tournamentId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task RegistrationsController_AssignCategoryAsync_RegeneratesAffectedUnlockedDraws()
    {
        var tournamentId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var oldCategoryId = Guid.NewGuid();
        var newCategoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var mockRegistrationsStore = new Mock<IRegistrationsStore>();
        var mockAthletesStore = new Mock<IAthletesStore>();
        var mockCategoriesStore = new Mock<ICategoriesStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        var mockBracketService = new Mock<IBracketService>();

        mockTournamentStore
            .Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));

        mockRegistrationsStore
            .Setup(s => s.GetByIdAsync(registrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Registration(registrationId, tournamentId, Guid.NewGuid(), oldCategoryId, now));

        mockCategoriesStore
            .Setup(s => s.GetByIdAsync(newCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(newCategoryId, tournamentId, "U18", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, BracketFormat.SingleElimination, false, now, now));

        mockCategoriesStore
            .Setup(s => s.GetByIdAsync(oldCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(oldCategoryId, tournamentId, "U16", "U16", Gender.Male, 66m, null, null, null, 300, false, 180, BracketFormat.SingleElimination, false, now, now));

        mockRegistrationsStore
            .Setup(s => s.AssignCategoryAsync(registrationId, newCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Registration(registrationId, tournamentId, Guid.NewGuid(), newCategoryId, now));

        mockBracketService
            .Setup(s => s.GenerateAsync(
                tournamentId,
                It.IsAny<Guid>(),
                BracketFormat.SingleElimination,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Fight>());

        var mockDokumePassParser = new Mock<IDokumePassParser>();
        var controller = new RegistrationsController(
            mockRegistrationsStore.Object,
            mockAthletesStore.Object,
            mockCategoriesStore.Object,
            mockTournamentStore.Object,
            mockBracketService.Object,
            mockDokumePassParser.Object,
            NullLogger<RegistrationsController>.Instance);

        var result = await controller.AssignCategoryAsync(
            tournamentId,
            registrationId,
            new AssignCategoryRequest { CategoryId = newCategoryId },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        mockBracketService.Verify(
            s => s.GenerateAsync(
                tournamentId,
                oldCategoryId,
                BracketFormat.SingleElimination,
                It.IsAny<CancellationToken>()),
            Times.Once);

        mockBracketService.Verify(
            s => s.GenerateAsync(
                tournamentId,
                newCategoryId,
                BracketFormat.SingleElimination,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

