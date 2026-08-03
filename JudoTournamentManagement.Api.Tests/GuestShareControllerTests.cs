using System.Security.Claims;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Controllers;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class GuestShareControllerTests
{
    private static Tournament SampleTournament(Guid tid) =>
        new(tid, "T", new DateOnly(2026, 7, 1), "V", "O", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static GuestShareState EnabledState(Guid tid, string token) =>
        new(tid, true, true, true, token, null);

    private static GuestShareController CreateController(
        Guid tid,
        Mock<IGuestShareService> service,
        Mock<IAuditLogService> audit,
        Mock<ITournamentStore>? tournaments = null,
        Mock<IGuestShareLinkBuilder>? links = null)
    {
        if (tournaments is null)
        {
            tournaments = new Mock<ITournamentStore>();
            tournaments.Setup(s => s.GetByIdAsync(tid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SampleTournament(tid));
        }

        if (links is null)
        {
            links = new Mock<IGuestShareLinkBuilder>();
            links.Setup(l => l.IsHostAllowedForSharing(It.IsAny<HttpRequest>())).Returns(true);
            links.Setup(l => l.BuildPublicUrl(It.IsAny<HttpRequest>(), It.IsAny<Guid>(), It.IsAny<string>()))
                .Returns("https://example.org/public/match-lists?t=abc&tid=1");
        }

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "operator1"), new Claim(ClaimTypes.Role, "Operator") },
            "Test"));

        return new GuestShareController(service.Object, tournaments.Object, links.Object, audit.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    [Fact]
    public async Task EnableAsync_EnablesShareAndLogsWithoutToken()
    {
        var tid = Guid.NewGuid();
        var token = "super-secret-token";
        var service = new Mock<IGuestShareService>();
        service.Setup(s => s.EnableAsync(tid, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnabledState(tid, token));
        var audit = new Mock<IAuditLogService>();
        var controller = CreateController(tid, service, audit);

        var result = await controller.EnableAsync(tid, new GuestShareRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GuestShareResponse>(ok.Value);
        Assert.True(response.IsActive);
        Assert.Equal(token, response.Token);
        Assert.NotNull(response.PublicUrl);
        audit.Verify(a => a.LogAsync(
            tid, "operator1", "GuestShareEnabled", "Tournament", tid,
            It.Is<string>(d => !d.Contains(token)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_LogsGuestShareDisabled()
    {
        var tid = Guid.NewGuid();
        var service = new Mock<IGuestShareService>();
        service.Setup(s => s.DisableAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuestShareState(tid, true, false, false, "tok", null));
        var audit = new Mock<IAuditLogService>();
        var controller = CreateController(tid, service, audit);

        var result = await controller.DisableAsync(tid, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        audit.Verify(a => a.LogAsync(
            tid, "operator1", "GuestShareDisabled", "Tournament", tid,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_LogsGuestShareRotated()
    {
        var tid = Guid.NewGuid();
        var service = new Mock<IGuestShareService>();
        service.Setup(s => s.RotateAsync(tid, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnabledState(tid, "new-token"));
        var audit = new Mock<IAuditLogService>();
        var controller = CreateController(tid, service, audit);

        var result = await controller.RotateAsync(tid, new GuestShareRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        audit.Verify(a => a.LogAsync(
            tid, "operator1", "GuestShareRotated", "Tournament", tid,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_WhenTournamentMissing_ReturnsNotFound()
    {
        var tid = Guid.NewGuid();
        var service = new Mock<IGuestShareService>();
        var audit = new Mock<IAuditLogService>();
        var tournaments = new Mock<ITournamentStore>();
        tournaments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tournament?)null);
        var controller = CreateController(tid, service, audit, tournaments);

        var result = await controller.EnableAsync(tid, new GuestShareRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        service.Verify(s => s.EnableAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetQrAsync_WhenNoShare_ReturnsNotFound()
    {
        var tid = Guid.NewGuid();
        var service = new Mock<IGuestShareService>();
        service.Setup(s => s.GetStateAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuestShareState(tid, false, false, false, null, null));
        var audit = new Mock<IAuditLogService>();
        var controller = CreateController(tid, service, audit);

        var result = await controller.GetQrAsync(tid, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetQrAsync_WhenShareExists_ReturnsSvgContent()
    {
        var tid = Guid.NewGuid();
        var service = new Mock<IGuestShareService>();
        service.Setup(s => s.GetStateAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnabledState(tid, "token123"));
        var audit = new Mock<IAuditLogService>();
        var controller = CreateController(tid, service, audit);

        var result = await controller.GetQrAsync(tid, CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("image/svg+xml", content.ContentType);
        Assert.Contains("<svg", content.Content);
    }

    [Fact]
    public async Task EnableAsync_WhenPublicHostWithoutTls_ReturnsBadRequestAndDoesNotEnable()
    {
        var tid = Guid.NewGuid();
        var service = new Mock<IGuestShareService>();
        var audit = new Mock<IAuditLogService>();
        var links = new Mock<IGuestShareLinkBuilder>();
        links.Setup(l => l.IsHostAllowedForSharing(It.IsAny<HttpRequest>())).Returns(false);
        var controller = CreateController(tid, service, audit, links: links);

        var result = await controller.EnableAsync(tid, new GuestShareRequest(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        service.Verify(s => s.EnableAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetQrAsync_WhenPublicHostWithoutTls_ReturnsBadRequest()
    {
        var tid = Guid.NewGuid();
        var service = new Mock<IGuestShareService>();
        service.Setup(s => s.GetStateAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnabledState(tid, "token123"));
        var audit = new Mock<IAuditLogService>();
        var links = new Mock<IGuestShareLinkBuilder>();
        links.Setup(l => l.IsHostAllowedForSharing(It.IsAny<HttpRequest>())).Returns(false);
        var controller = CreateController(tid, service, audit, links: links);

        var result = await controller.GetQrAsync(tid, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }
}
