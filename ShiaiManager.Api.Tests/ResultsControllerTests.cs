using ShiaiManager.Api.Controllers;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ShiaiManager.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class ResultsControllerTests
{
    [Fact]
    public async Task GetAgeGroupClubScoringAsync_UnknownTournament_ReturnsNotFound()
    {
        var tournamentStore = new Mock<ITournamentStore>();
        tournamentStore
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tournament?)null);

        var rankingService = new Mock<IRankingService>(MockBehavior.Strict);

        var controller = CreateController(rankingService.Object, tournamentStore.Object);

        var result = await controller.GetAgeGroupClubScoringAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetGlobalClubScoringAsync_UnknownTournament_ReturnsNotFound()
    {
        var tournamentStore = new Mock<ITournamentStore>();
        tournamentStore
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tournament?)null);

        var rankingService = new Mock<IRankingService>(MockBehavior.Strict);

        var controller = CreateController(rankingService.Object, tournamentStore.Object);

        var result = await controller.GetGlobalClubScoringAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetAgeGroupClubScoringAsync_ExistingTournament_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new Tournament(
            tournamentId,
            "T",
            new DateOnly(2026, 7, 30),
            "V",
            "O",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var payload = new AgeGroupClubScoringResponse(
            tournamentId,
            DateTimeOffset.UtcNow,
            [
                new AgeGroupClubScoringItem(
                    "U15",
                    "Provisional",
                    1,
                    2,
                    [
                        new ClubScoringEntry(
                            1,
                            false,
                            Guid.NewGuid(),
                            "A",
                            1,
                            0,
                            0,
                            7,
                            1,
                            1,
                            1m,
                            1m,
                            7m,
                            7m)
                    ])
            ]);

        var tournamentStore = new Mock<ITournamentStore>();
        tournamentStore
            .Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);

        var rankingService = new Mock<IRankingService>();
        rankingService
            .Setup(s => s.GetAgeGroupClubScoringAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var controller = CreateController(rankingService.Object, tournamentStore.Object);

        var result = await controller.GetAgeGroupClubScoringAsync(tournamentId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var body = Assert.IsType<AgeGroupClubScoringResponse>(ok.Value);
        Assert.Equal(tournamentId, body.TournamentId);
    }

    [Fact]
    public async Task GetGlobalClubScoringAsync_ExistingTournament_ReturnsOk()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new Tournament(
            tournamentId,
            "T",
            new DateOnly(2026, 7, 30),
            "V",
            "O",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var payload = new GlobalClubScoringResponse(
            tournamentId,
            DateTimeOffset.UtcNow,
            "Final",
            10,
            10,
            [
                new ClubScoringEntry(
                    1,
                    false,
                    Guid.NewGuid(),
                    "A",
                    1,
                    0,
                    0,
                    7,
                    1,
                    1,
                    1m,
                    1m,
                    7m,
                    7m)
            ]);

        var tournamentStore = new Mock<ITournamentStore>();
        tournamentStore
            .Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);

        var rankingService = new Mock<IRankingService>();
        rankingService
            .Setup(s => s.GetGlobalClubScoringAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var controller = CreateController(rankingService.Object, tournamentStore.Object);

        var result = await controller.GetGlobalClubScoringAsync(tournamentId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var body = Assert.IsType<GlobalClubScoringResponse>(ok.Value);
        Assert.Equal(tournamentId, body.TournamentId);
    }

    [Fact]
    public async Task ExportCsvAsync_UnknownTournament_ReturnsNotFound()
    {
        var tournamentStore = new Mock<ITournamentStore>();
        tournamentStore
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tournament?)null);

        var rankingService = new Mock<IRankingService>(MockBehavior.Strict);
        var exportService = new Mock<IResultsCsvExportService>(MockBehavior.Strict);
        var controller = new ResultsController(rankingService.Object, tournamentStore.Object, exportService.Object);

        var result = await controller.ExportCsvAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        exportService.Verify(
            service => service.CreateAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExportCsvAsync_ExistingTournament_ReturnsCsvFile()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = new Tournament(
            tournamentId,
            "Sommerpokal",
            new DateOnly(2026, 7, 30),
            "Essen",
            "Judo Verein",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var tournamentStore = new Mock<ITournamentStore>();
        tournamentStore
            .Setup(s => s.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);

        var rankingService = new Mock<IRankingService>(MockBehavior.Strict);
        var exportService = new Mock<IResultsCsvExportService>();
        var expected = new ResultsCsvExport([0xEF, 0xBB, 0xBF, 0x41], "ergebnisse-Sommerpokal.csv");
        exportService
            .Setup(service => service.CreateAsync(tournament, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new ResultsController(rankingService.Object, tournamentStore.Object, exportService.Object);

        var result = await controller.ExportCsvAsync(tournamentId, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);
        Assert.Equal(expected.FileName, file.FileDownloadName);
        Assert.Equal(expected.Content, file.FileContents);
    }

    private static ResultsController CreateController(IRankingService rankingService, ITournamentStore tournamentStore)
    {
        return new ResultsController(
            rankingService,
            tournamentStore,
            Mock.Of<IResultsCsvExportService>());
    }
}
