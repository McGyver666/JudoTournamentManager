using System.Security.Claims;
using JudoTournamentManagement.Api.Hubs;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class TournamentHubTests
{
    private static Tournament SampleTournament(Guid tid) =>
        new(tid, "T", new DateOnly(2026, 7, 1), "V", "O", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static ClaimsPrincipal GuestPrincipal(Guid tid) =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Role, IGuestShareService.GuestRole),
                new Claim(IGuestShareService.TournamentClaimType, tid.ToString())
            },
            "Test"));

    private static ClaimsPrincipal OperatorPrincipal() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Operator") }, "Test"));

    private static (TournamentHub Hub, Mock<IGroupManager> Groups) CreateHub(
        ClaimsPrincipal user,
        Mock<ITournamentStore> tournaments,
        Mock<IGuestShareService> shares)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns("conn-1");
        context.SetupGet(c => c.User).Returns(user);
        context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);

        var groups = new Mock<IGroupManager>();

        var hub = new TournamentHub(tournaments.Object, shares.Object)
        {
            Context = context.Object,
            Groups = groups.Object
        };

        return (hub, groups);
    }

    [Fact]
    public async Task JoinTournament_WhenOperator_JoinsGroup()
    {
        var tid = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        tournaments.Setup(s => s.GetByIdAsync(tid, It.IsAny<CancellationToken>())).ReturnsAsync(SampleTournament(tid));
        var shares = new Mock<IGuestShareService>();
        var (hub, groups) = CreateHub(OperatorPrincipal(), tournaments, shares);

        await hub.JoinTournamentAsync(tid.ToString());

        groups.Verify(g => g.AddToGroupAsync("conn-1", tid.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        shares.Verify(s => s.GetStateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinTournament_WhenGuestScopedAndActive_JoinsGroup()
    {
        var tid = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        tournaments.Setup(s => s.GetByIdAsync(tid, It.IsAny<CancellationToken>())).ReturnsAsync(SampleTournament(tid));
        var shares = new Mock<IGuestShareService>();
        shares.Setup(s => s.GetStateAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuestShareState(tid, true, true, true, "tok", null));
        var (hub, groups) = CreateHub(GuestPrincipal(tid), tournaments, shares);

        await hub.JoinTournamentAsync(tid.ToString());

        groups.Verify(g => g.AddToGroupAsync("conn-1", tid.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinTournament_WhenGuestScopedToDifferentTournament_Throws()
    {
        var routeTid = Guid.NewGuid();
        var guestTid = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        var shares = new Mock<IGuestShareService>();
        var (hub, groups) = CreateHub(GuestPrincipal(guestTid), tournaments, shares);

        await Assert.ThrowsAsync<HubException>(() => hub.JoinTournamentAsync(routeTid.ToString()));

        groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinTournament_WhenGuestShareInactive_Throws()
    {
        var tid = Guid.NewGuid();
        var tournaments = new Mock<ITournamentStore>();
        var shares = new Mock<IGuestShareService>();
        shares.Setup(s => s.GetStateAsync(tid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuestShareState(tid, true, false, false, "tok", null));
        var (hub, groups) = CreateHub(GuestPrincipal(tid), tournaments, shares);

        await Assert.ThrowsAsync<HubException>(() => hub.JoinTournamentAsync(tid.ToString()));

        groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
