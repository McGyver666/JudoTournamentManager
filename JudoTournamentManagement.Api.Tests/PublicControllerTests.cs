using System.Security.Claims;
using JudoTournamentManagement.Api.Controllers;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class PublicControllerTests
{
    private static ClaimsPrincipal GuestPrincipal(Guid tournamentId)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Role, IGuestShareService.GuestRole),
                new Claim(IGuestShareService.TournamentClaimType, tournamentId.ToString())
            },
            "TestGuest");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal OperatorPrincipal()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Operator") }, "TestOperator");
        return new ClaimsPrincipal(identity);
    }

    private static PublicController CreateController(
        ClaimsPrincipal user,
        Mock<ITournamentStore>? tournaments = null,
        Mock<IAthletesStore>? athletes = null,
        Mock<IClubsStore>? clubs = null,
        Mock<ICategoriesStore>? categories = null,
        Mock<IFightsStore>? fights = null,
        Mock<IRankingService>? ranking = null)
    {
        var controller = new PublicController(
            (tournaments ?? new Mock<ITournamentStore>()).Object,
            (athletes ?? new Mock<IAthletesStore>()).Object,
            (clubs ?? new Mock<IClubsStore>()).Object,
            (categories ?? new Mock<ICategoriesStore>()).Object,
            (fights ?? new Mock<IFightsStore>()).Object,
            (ranking ?? new Mock<IRankingService>()).Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
        return controller;
    }

    private static Athlete SampleAthlete(Guid tournamentId, Guid clubId) => new(
        Guid.NewGuid(), tournamentId, clubId, "Max", "Mustermann",
        2008, Gender.Male, "LIC-123", 60m, 5, null, null,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task GetAthletes_WhenGuestScopedToTournament_ReturnsReducedProjection()
    {
        var tid = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        tournaments.Setup(s => s.GetByIdAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tid, "T", new DateOnly(2026, 7, 1), "V", "O", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var athletes = new Mock<IAthletesStore>();
        athletes.Setup(s => s.GetAllAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Athlete> { SampleAthlete(tid, clubId) });
        var controller = CreateController(GuestPrincipal(tid), tournaments, athletes);

        var result = await controller.GetAthletesAsync(tid, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<PublicAthlete>>(ok.Value);
        var athlete = Assert.Single(payload);
        Assert.Equal("Max", athlete.FirstName);
        Assert.Equal("Mustermann", athlete.LastName);
        Assert.Equal(clubId, athlete.ClubId);
    }

    [Fact]
    public async Task GetAthletes_WhenGuestScopedToDifferentTournament_ReturnsForbid()
    {
        var routeTid = Guid.NewGuid();
        var otherTid = Guid.NewGuid();
        var controller = CreateController(GuestPrincipal(otherTid));

        var result = await controller.GetAthletesAsync(routeTid, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetClubs_WhenGuestScoped_ReturnsReducedProjection()
    {
        var tid = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        tournaments.Setup(s => s.GetByIdAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tid, "T", new DateOnly(2026, 7, 1), "V", "O", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var clubs = new Mock<IClubsStore>();
        clubs.Setup(s => s.GetAllAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Club>
            {
                new(clubId, tid, "JC Essen", "Kontakt", "mail@example.org", "0123", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            });
        var controller = CreateController(GuestPrincipal(tid), tournaments, clubs: clubs);

        var result = await controller.GetClubsAsync(tid, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<PublicClub>>(ok.Value);
        var club = Assert.Single(payload);
        Assert.Equal(clubId, club.Id);
        Assert.Equal("JC Essen", club.Name);
    }

    [Fact]
    public async Task GetAthletes_WhenOperator_IgnoresGuestScope()
    {
        var tid = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        tournaments.Setup(s => s.GetByIdAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tid, "T", new DateOnly(2026, 7, 1), "V", "O", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var athletes = new Mock<IAthletesStore>();
        athletes.Setup(s => s.GetAllAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Athlete>());
        var controller = CreateController(OperatorPrincipal(), tournaments, athletes);

        var result = await controller.GetAthletesAsync(tid, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAthletes_WhenTournamentMissing_ReturnsNotFound()
    {
        var tid = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        tournaments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tournament?)null);
        var controller = CreateController(GuestPrincipal(tid), tournaments);

        var result = await controller.GetAthletesAsync(tid, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetFights_WhenCategoryFromOtherTournament_ReturnsNotFound()
    {
        var tid = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var categories = new Mock<ICategoriesStore>();
        categories.Setup(s => s.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(
                categoryId, Guid.NewGuid(), "U18", "U18", Gender.Male, 73m, null, null, null,
                300, false, 180, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var controller = CreateController(GuestPrincipal(tid), categories: categories);

        var result = await controller.GetFightsAsync(tid, categoryId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetTournament_WhenGuestScoped_ReturnsTournament()
    {
        var tid = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        tournaments.Setup(s => s.GetByIdAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tournament(tid, "Sommer-Cup", new DateOnly(2026, 7, 1), "V", "O", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var controller = CreateController(GuestPrincipal(tid), tournaments);

        var result = await controller.GetTournamentAsync(tid, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<Tournament>(ok.Value);
        Assert.Equal(tid, payload.Id);
        Assert.Equal("Sommer-Cup", payload.Name);
    }

    [Fact]
    public async Task GetTournament_WhenGuestScopedToDifferentTournament_ReturnsForbid()
    {
        var routeTid = Guid.NewGuid();
        var otherTid = Guid.NewGuid();
        var controller = CreateController(GuestPrincipal(otherTid));

        var result = await controller.GetTournamentAsync(routeTid, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetTournament_WhenTournamentMissing_ReturnsNotFound()
    {
        var tid = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        tournaments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tournament?)null);
        var controller = CreateController(GuestPrincipal(tid), tournaments);

        var result = await controller.GetTournamentAsync(tid, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
