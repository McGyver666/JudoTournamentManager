using System.ComponentModel.DataAnnotations;
using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Controllers;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ShiaiManager.Api.Tests;

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
            It.IsAny<bool>(),
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
            request.TwoThirdPlacesInRoundRobin,
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
            It.IsAny<bool>(),
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
            mockCategoryGenerationService.Object,
            new Mock<IAuditLogService>().Object);

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
            mockCategoryGenerationService.Object,
            new Mock<IAuditLogService>().Object);
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
            mockCategoryGenerationService.Object,
            new Mock<IAuditLogService>().Object);

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
    public async Task CategoriesController_ApplyGenerationAsync_WithValidData_ReturnsOkAndWritesAuditEntry()
    {
        var tournamentId = Guid.NewGuid();
        var mockCategoriesStore = new Mock<ICategoriesStore>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        var mockCategoryGenerationService = new Mock<ICategoryGenerationService>();
        var mockAuditLog = new Mock<IAuditLogService>();

        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        mockCategoryGenerationService.Setup(s => s.ApplyAsync(
                tournamentId,
                It.IsAny<GenerateCategoriesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CategoryGenerationApplyResponse(3, 2, 0, [], []));

        var controller = new CategoriesController(
            mockCategoriesStore.Object,
            mockTournamentStore.Object,
            mockCategoryGenerationService.Object,
            mockAuditLog.Object);

        var request = new GenerateCategoriesRequest
        {
            AgeGroup = "U13",
            GenderMode = CategoryGenerationGenderMode.Male,
            WeightMode = CategoryGenerationWeightMode.StandardClasses
        };

        var result = await controller.ApplyGenerationAsync(tournamentId, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        mockAuditLog.Verify(a => a.LogAsync(
            tournamentId,
            "system",
            "CategoriesGenerated",
            "Category",
            null,
            "U13 Male: deleted=2, created=3",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CategoriesController_ApplyGenerationAsync_WhenRejected_ReturnsMessageKeyAndSkipsAudit()
    {
        var tournamentId = Guid.NewGuid();
        var mockTournamentStore = new Mock<ITournamentStore>();
        var mockCategoryGenerationService = new Mock<ICategoryGenerationService>();
        var mockAuditLog = new Mock<IAuditLogService>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockCategoryGenerationService.Setup(s => s.ApplyAsync(
                tournamentId,
                It.IsAny<GenerateCategoriesRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LocalizedOperationException(AgeGroupMessages.CategoriesCannotBeReplaced));
        var controller = new CategoriesController(
            new Mock<ICategoriesStore>().Object,
            mockTournamentStore.Object,
            mockCategoryGenerationService.Object,
            mockAuditLog.Object);

        var result = await controller.ApplyGenerationAsync(
            tournamentId,
            new GenerateCategoriesRequest
            {
                AgeGroup = "U13",
                GenderMode = CategoryGenerationGenderMode.Male,
                WeightMode = CategoryGenerationWeightMode.StandardClasses
            },
            CancellationToken.None);

        AssertMessageKey(result.Result, AgeGroupMessages.CategoriesCannotBeReplaced.Key);
        mockAuditLog.Verify(a => a.LogAsync(
            It.IsAny<Guid?>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
        var mockDmfParser = new Mock<IDmfAthleteImportParser>();
        var mockTournamentStore = new Mock<ITournamentStore>();
        mockTournamentStore.Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        mockAthletesStore.Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Athlete>());
        var controller = new AthletesController(
            mockAthletesStore.Object,
            mockClubsStore.Object,
            mockDm4Parser.Object,
            mockDmfParser.Object,
            mockTournamentStore.Object);

        var result = await controller.GetAllAsync(tournamentId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task AthletesController_CreateAsync_WithNullGrade_PassesNullToStore()
    {
        var tournamentId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var mockAthletesStore = new Mock<IAthletesStore>();
        var mockClubsStore = new Mock<IClubsStore>();
        var mockDm4Parser = new Mock<IDm4AthleteImportParser>();
        var mockDmfParser = new Mock<IDmfAthleteImportParser>();
        var mockTournamentStore = new Mock<ITournamentStore>();

        mockTournamentStore
            .Setup(store => store.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));
        mockClubsStore
            .Setup(store => store.GetByIdAsync(clubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Club(clubId, tournamentId, "Test Club", null, null, null, now, now));
        mockAthletesStore
            .Setup(store => store.CreateAsync(
                tournamentId, clubId, "Anna", "Schmidt", 2005, Gender.Female, null, null, null, false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Athlete(athleteId, tournamentId, clubId, "Anna", "Schmidt", 2005, Gender.Female,
                null, null, null, null, null, now, now));

        var controller = new AthletesController(
            mockAthletesStore.Object,
            mockClubsStore.Object,
            mockDm4Parser.Object,
            mockDmfParser.Object,
            mockTournamentStore.Object);

        var result = await controller.CreateAsync(tournamentId, new CreateAthleteRequest
        {
            ClubId = clubId,
            FirstName = "Anna",
            LastName = "Schmidt",
            BirthYear = 2005,
            Gender = Gender.Female,
            Grade = null,
        }, false, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
        mockAthletesStore.Verify(store => store.CreateAsync(
            tournamentId, clubId, "Anna", "Schmidt", 2005, Gender.Female, null, null, null, false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateAthleteRequest_AllowsNullGradeAndRejectsOutOfRangeGrade()
    {
        var request = new CreateAthleteRequest
        {
            ClubId = Guid.NewGuid(),
            FirstName = "Anna",
            LastName = "Schmidt",
            BirthYear = 2005,
            Gender = Gender.Female,
            Grade = null,
        };
        var nullableResults = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), nullableResults, true));

        var invalidRequest = request with { Grade = 15 };
        var invalidResults = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(invalidRequest, new ValidationContext(invalidRequest), invalidResults, true));
        Assert.Contains(invalidResults, result => result.MemberNames.Contains(nameof(CreateAthleteRequest.Grade)));
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
        var mockDmfParser = new Mock<IDmfAthleteImportParser>();
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
            mockDmfParser.Object,
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
        var mockDmfParser = new Mock<IDmfAthleteImportParser>();
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
            mockDmfParser.Object,
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
        var mockDmfParser = new Mock<IDmfAthleteImportParser>();
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
            mockDmfParser.Object,
            mockTournamentStore.Object);

        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "athletes.dm4");

        var result = await controller.ImportFromDm4Async(tournamentId, file, false, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains("Formatfehler", controller.ModelState[nameof(file)]!.Errors.Select(x => x.ErrorMessage));
    }

    [Fact]
    public async Task AthletesController_ImportFromFileAsync_WithDmfFile_UsesDmfParser()
    {
        var tournamentId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var mockAthletesStore = new Mock<IAthletesStore>();
        var mockClubsStore = new Mock<IClubsStore>();
        var mockDm4Parser = new Mock<IDm4AthleteImportParser>();
        var mockDmfParser = new Mock<IDmfAthleteImportParser>();
        var mockTournamentStore = new Mock<ITournamentStore>();

        mockTournamentStore
            .Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));

        mockClubsStore
            .Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Club(clubId, tournamentId, "DJK Sportfreunde Dülmen", null, null, null, now, now)]);

        mockDmfParser
            .Setup(x => x.Parse(It.IsAny<ReadOnlyMemory<byte>>(), "athletes.dmf"))
            .Returns(new Dm4AthleteImportData(
                "DJK Sportfreunde Dülmen",
                "Klaus Schulze Temming",
                null,
                "02594 86643",
                Gender.Male,
                [new Dm4AthleteImportRow("Ciunta", "Raul Emanuel", 1, 60m, 2003)]));

        mockAthletesStore
            .Setup(s => s.CreateBulkAsync(
                tournamentId,
                It.IsAny<IReadOnlyList<AthleteImportItem>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Athlete(Guid.NewGuid(), tournamentId, clubId, "Raul Emanuel", "Ciunta", 2003, Gender.Male, null, 60m, 1, null, null, now, now)
            ]);

        var controller = new AthletesController(
            mockAthletesStore.Object,
            mockClubsStore.Object,
            mockDm4Parser.Object,
            mockDmfParser.Object,
            mockTournamentStore.Object);

        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "athletes.dmf");

        var result = await controller.ImportFromFileAsync(tournamentId, file, false, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        mockDmfParser.Verify(x => x.Parse(It.IsAny<ReadOnlyMemory<byte>>(), "athletes.dmf"), Times.Once);
        mockDm4Parser.Verify(x => x.Parse(It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
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
            new Mock<ICategoryPresetsStore>().Object,
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
        var athleteId = Guid.NewGuid();
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
            .ReturnsAsync(new Registration(registrationId, tournamentId, athleteId, oldCategoryId, now));

        mockRegistrationsStore
            .Setup(s => s.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RegistrationDetail(
                    registrationId, tournamentId, athleteId, "Person", "Test", 2005, Gender.Male, "Club",
                    oldCategoryId, "U16", "U16", Gender.Male, 66m, 65m, true, now)
            ]);

        mockCategoriesStore
            .Setup(s => s.GetByIdAsync(newCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(newCategoryId, tournamentId, "U18", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, BracketFormat.SingleElimination, false, now, now));

        mockCategoriesStore
            .Setup(s => s.GetByIdAsync(oldCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(oldCategoryId, tournamentId, "U16", "U16", Gender.Male, 66m, null, null, null, 300, false, 180, BracketFormat.SingleElimination, false, now, now));

        mockCategoriesStore
            .Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Category(newCategoryId, tournamentId, "U18", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, BracketFormat.SingleElimination, false, now, now)]);

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
        var mockCategoryPresetsStore = new Mock<ICategoryPresetsStore>();
        mockCategoryPresetsStore
            .Setup(s => s.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TournamentCategoryPreset(
                    Guid.NewGuid(), tournamentId, "U18", Gender.Male, 120, 1, 1906, 2025, 300, [73m, null], 0)
            ]);
        var controller = new RegistrationsController(
            mockRegistrationsStore.Object,
            mockAthletesStore.Object,
            mockCategoriesStore.Object,
            mockTournamentStore.Object,
            mockCategoryPresetsStore.Object,
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

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task RegistrationsController_UpdateStartAgeGroupAsync_UnassignsCategoryWhenEffectiveAgeGroupChanges()
    {
        var tournamentId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var currentCategory = new Category(categoryId, tournamentId, "U11 W -40", "U11", Gender.Female,
            40m, 2016, 2018, null, 120, false, 180, null, false, now, now);
        var registrationsStore = new Mock<IRegistrationsStore>();
        var categoriesStore = new Mock<ICategoriesStore>();
        var tournamentStore = new Mock<ITournamentStore>();
        var presetsStore = new Mock<ICategoryPresetsStore>();
        var bracketService = new Mock<IBracketService>();
        var registration = new Registration(registrationId, tournamentId, athleteId, categoryId, now);

        tournamentStore
            .Setup(x => x.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));
        registrationsStore
            .Setup(x => x.GetByIdAsync(registrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RegistrationDetail(registrationId, tournamentId, athleteId, "Person", "Test", 2016,
                    Gender.Female, "Club", categoryId, currentCategory.Name, "U11", Gender.Female, 40m, 29m, true, now)
            ]);
        categoriesStore
            .Setup(x => x.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCategory);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TournamentCategoryPreset(Guid.NewGuid(), tournamentId, "U11", Gender.Female, 10, 8, 2016, 2018, 120, [], 0),
                new TournamentCategoryPreset(Guid.NewGuid(), tournamentId, "U13", Gender.Female, 12, 10, 2014, 2016, 180, [40m, null], 1)
            ]);
        registrationsStore
            .Setup(x => x.UpdateStartAgeGroupAsync(registrationId, "U13", null, "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration with { CategoryId = null, StartAgeGroup = "U13" });

        var controller = new RegistrationsController(
            registrationsStore.Object,
            new Mock<IAthletesStore>().Object,
            categoriesStore.Object,
            tournamentStore.Object,
            presetsStore.Object,
            bracketService.Object,
            new Mock<IDokumePassParser>().Object,
            NullLogger<RegistrationsController>.Instance);

        var result = await controller.UpdateStartAgeGroupAsync(
            tournamentId,
            registrationId,
            new UpdateRegistrationStartAgeGroupRequest { StartAgeGroup = "U13" },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        registrationsStore.Verify(
            x => x.UpdateStartAgeGroupAsync(registrationId, "U13", null, "system", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task RegistrationsController_UpdateStartAgeGroupAsync_RejectsDrawnCategory()
    {
        var tournamentId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var registration = new Registration(registrationId, tournamentId, athleteId, categoryId, now);
        var registrationsStore = new Mock<IRegistrationsStore>();
        var categoriesStore = new Mock<ICategoriesStore>();
        var tournamentStore = new Mock<ITournamentStore>();
        var presetsStore = new Mock<ICategoryPresetsStore>();
        var drawnCategory = new Category(categoryId, tournamentId, "U11 W -40", "U11", Gender.Female,
            40m, 2016, 2018, null, 120, false, 180, BracketFormat.SingleElimination, false, now, now);

        tournamentStore
            .Setup(x => x.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));
        registrationsStore
            .Setup(x => x.GetByIdAsync(registrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RegistrationDetail(registrationId, tournamentId, athleteId, "Person", "Test", 2016,
                    Gender.Female, "Club", categoryId, drawnCategory.Name, "U11", Gender.Female, 40m, 29m, true, now)
            ]);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TournamentCategoryPreset(Guid.NewGuid(), tournamentId, "U11", Gender.Female, 10, 8, 2016, 2018, 120, [], 0),
                new TournamentCategoryPreset(Guid.NewGuid(), tournamentId, "U13", Gender.Female, 12, 10, 2014, 2016, 180, [40m, null], 1)
            ]);
        categoriesStore
            .Setup(x => x.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(drawnCategory);

        var controller = new RegistrationsController(
            registrationsStore.Object,
            new Mock<IAthletesStore>().Object,
            categoriesStore.Object,
            tournamentStore.Object,
            presetsStore.Object,
            new Mock<IBracketService>().Object,
            new Mock<IDokumePassParser>().Object,
            NullLogger<RegistrationsController>.Instance);

        var result = await controller.UpdateStartAgeGroupAsync(
            tournamentId,
            registrationId,
            new UpdateRegistrationStartAgeGroupRequest { StartAgeGroup = "U13" },
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        registrationsStore.Verify(
            x => x.UpdateStartAgeGroupAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task RegistrationsController_CreateAsync_WithStartAgeGroupNotCoveringBirthYear_ReturnsMessageKey()
    {
        var tournamentId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var athletesStore = new Mock<IAthletesStore>();
        var tournamentStore = new Mock<ITournamentStore>();
        var presetsStore = new Mock<ICategoryPresetsStore>();
        var registrationsStore = new Mock<IRegistrationsStore>(MockBehavior.Strict);
        tournamentStore
            .Setup(x => x.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));
        athletesStore
            .Setup(x => x.GetByIdAsync(athleteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Athlete(athleteId, tournamentId, Guid.NewGuid(), "Anna", "Schmidt", 2016, Gender.Female,
                null, null, null, null, null, now, now));
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssignmentPresets(tournamentId));

        var controller = new RegistrationsController(
            registrationsStore.Object,
            athletesStore.Object,
            new Mock<ICategoriesStore>().Object,
            tournamentStore.Object,
            presetsStore.Object,
            new Mock<IBracketService>().Object,
            new Mock<IDokumePassParser>().Object,
            NullLogger<RegistrationsController>.Instance);

        var result = await controller.CreateAsync(
            tournamentId,
            new CreateRegistrationRequest { AthleteId = athleteId, WeightKg = 30m, StartAgeGroup = "U15" },
            CancellationToken.None);

        AssertMessageKey(result.Result, AgeGroupMessages.StartAgeGroupInvalid.Key);
    }

    [Theory]
    [Trait("Category", "UnitTest")]
    [InlineData("U13", Gender.Female, 40, "errors.ageGroups.categoryAgeGroupMismatch")]
    [InlineData("U11", Gender.Male, 40, "errors.ageGroups.categoryGenderMismatch")]
    [InlineData("U11", Gender.Female, 28, "errors.ageGroups.weightAboveCategoryLimit")]
    public async Task RegistrationsController_AssignCategoryAsync_RejectsCategoryOutsideEffectiveAgeGroupGenderOrWeight(
        string categoryAgeGroup,
        Gender categoryGender,
        int categoryWeightLimitKg,
        string expectedMessageKey)
    {
        var tournamentId = Guid.NewGuid();
        var category = AssignmentCategory(tournamentId, categoryAgeGroup, categoryGender, categoryWeightLimitKg, 2016, 2018);
        var scenario = CreateAssignmentScenario(tournamentId, category, 2016, Gender.Female, 29m);

        var result = await scenario.Controller.AssignCategoryAsync(
            tournamentId,
            scenario.RegistrationId,
            new AssignCategoryRequest { CategoryId = category.Id },
            CancellationToken.None);

        AssertMessageKey(result.Result, expectedMessageKey);
        scenario.Registrations.Verify(
            x => x.AssignCategoryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [Trait("Category", "UnitTest")]
    [InlineData(29.0)]
    [InlineData(null)]
    public async Task RegistrationsController_AssignCategoryAsync_TreatsBirthYearRangeAsPlausibilityAndAllowsHeavierClass(
        double? athleteWeightKg)
    {
        var tournamentId = Guid.NewGuid();
        // Birth year 2016 lies outside 2017-2018, and -40 kg is heavier than the athlete needs.
        var category = AssignmentCategory(tournamentId, "U11", Gender.Female, 40, 2017, 2018);
        var scenario = CreateAssignmentScenario(
            tournamentId, category, 2016, Gender.Female, (decimal?)athleteWeightKg);

        var result = await scenario.Controller.AssignCategoryAsync(
            tournamentId,
            scenario.RegistrationId,
            new AssignCategoryRequest { CategoryId = category.Id },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        scenario.Registrations.Verify(
            x => x.AssignCategoryAsync(scenario.RegistrationId, category.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task RegistrationsController_AssignCategoryAsync_UsesStartAgeGroupForHigherStarter()
    {
        var tournamentId = Guid.NewGuid();
        var category = AssignmentCategory(tournamentId, "U13", Gender.Female, 40, 2014, 2016);
        var scenario = CreateAssignmentScenario(tournamentId, category, 2016, Gender.Female, 29m, "U13");

        var result = await scenario.Controller.AssignCategoryAsync(
            tournamentId,
            scenario.RegistrationId,
            new AssignCategoryRequest { CategoryId = category.Id },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    private static void AssertMessageKey(IActionResult? result, string expectedMessageKey)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedMessageKey, problem.Extensions["messageKey"]);
    }

    private static TournamentCategoryPreset[] AssignmentPresets(Guid tournamentId) =>
    [
        new(Guid.NewGuid(), tournamentId, "U11", Gender.Female, 10, 8, 2016, 2018, 120, [30m, 40m, null], 0),
        new(Guid.NewGuid(), tournamentId, "U13", Gender.Female, 12, 10, 2014, 2016, 180, [40m, null], 1),
        new(Guid.NewGuid(), tournamentId, "U11", Gender.Male, 10, 8, 2016, 2018, 120, [30m, 40m, null], 2)
    ];

    private static Category AssignmentCategory(
        Guid tournamentId,
        string ageGroup,
        Gender gender,
        decimal weightLimitKg,
        int minBirthYear,
        int maxBirthYear)
    {
        var now = DateTimeOffset.UtcNow;
        return new Category(Guid.NewGuid(), tournamentId, $"{ageGroup} -{weightLimitKg}", ageGroup, gender,
            weightLimitKg, minBirthYear, maxBirthYear, null, 120, false, 180, null, false, now, now);
    }

    private static AssignmentScenario CreateAssignmentScenario(
        Guid tournamentId,
        Category category,
        int birthYear,
        Gender gender,
        decimal? weightKg,
        string? startAgeGroup = null)
    {
        var now = DateTimeOffset.UtcNow;
        var registrationId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var registrationsStore = new Mock<IRegistrationsStore>();
        var categoriesStore = new Mock<ICategoriesStore>();
        var tournamentStore = new Mock<ITournamentStore>();
        var presetsStore = new Mock<ICategoryPresetsStore>();

        tournamentStore
            .Setup(x => x.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tournamentId, "Test", new DateOnly(2026, 7, 15), "Venue", "Org", now, now));
        registrationsStore
            .Setup(x => x.GetByIdAsync(registrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Registration(registrationId, tournamentId, athleteId, null, now) { StartAgeGroup = startAgeGroup });
        registrationsStore
            .Setup(x => x.GetDetailedAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RegistrationDetail(registrationId, tournamentId, athleteId, "Person", "Test", birthYear, gender,
                    "Club", null, null, null, null, null, weightKg, true, now)
                {
                    StartAgeGroup = startAgeGroup
                }
            ]);
        registrationsStore
            .Setup(x => x.AssignCategoryAsync(registrationId, category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Registration(registrationId, tournamentId, athleteId, category.Id, now));
        categoriesStore
            .Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        presetsStore
            .Setup(x => x.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssignmentPresets(tournamentId));

        var controller = new RegistrationsController(
            registrationsStore.Object,
            new Mock<IAthletesStore>().Object,
            categoriesStore.Object,
            tournamentStore.Object,
            presetsStore.Object,
            new Mock<IBracketService>().Object,
            new Mock<IDokumePassParser>().Object,
            NullLogger<RegistrationsController>.Instance);
        return new AssignmentScenario(controller, registrationsStore, registrationId);
    }

    private sealed record AssignmentScenario(
        RegistrationsController Controller,
        Mock<IRegistrationsStore> Registrations,
        Guid RegistrationId);

    #endregion
}

