using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Controllers;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class AuthControllerTests
{
    [Fact]
    public async Task BootstrapAdminAsync_WhenCreated_Returns201()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.BootstrapAdminAsync("admin", "Strong!Pass123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootstrapAdminResult(true, []));

        var controller = new AuthController(auth.Object);
        var result = await controller.BootstrapAdminAsync(
            new BootstrapAdminRequest { UserName = "admin", Password = "Strong!Pass123" },
            CancellationToken.None);

        var created = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WhenInvalid_Returns401()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.LoginAsync("admin", "wrong", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(LoginStatus.InvalidCredentials, null, null, null, null));

        var controller = new AuthController(auth.Object);
        var result = await controller.LoginAsync(
            new LoginRequest { UserName = "admin", Password = "wrong" },
            CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WhenSuccess_ReturnsTokenPayload()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.LoginAsync("admin", "Strong!Pass123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(
                LoginStatus.Success,
                "token123",
                DateTimeOffset.UtcNow.AddHours(1),
                "admin",
                "Admin"));

        var controller = new AuthController(auth.Object);
        var result = await controller.LoginAsync(
            new LoginRequest { UserName = "admin", Password = "Strong!Pass123" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.IsType<LoginResponse>(ok.Value);
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsOk()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocalUserAccount>());

        var controller = new AuthController(auth.Object);
        var result = await controller.GetUsersAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task CreateUserAsync_WhenCreated_Returns201()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.CreateUserAsync(It.IsAny<string>(), "operator1", "Operator", "Operator!1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateUserResult(true, Guid.NewGuid(), []));

        var controller = new AuthController(auth.Object)
        {
            ControllerContext = BuildControllerContext("admin", "Admin")
        };

        var result = await controller.CreateUserAsync(
            new CreateUserRequest { UserName = "operator1", Role = "Operator", Password = "Operator!1234" },
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
    }

    [Fact]
    public async Task SetUserActiveStateAsync_WhenNotFound_Returns404()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.SetUserActiveStateAsync(It.IsAny<string>(), It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateUserStateResult(false, "NotFound", "not found"));

        var controller = new AuthController(auth.Object)
        {
            ControllerContext = BuildControllerContext("admin", "Admin")
        };

        var result = await controller.SetUserActiveStateAsync(Guid.NewGuid(), new SetUserActiveRequest(false), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenValidationFails_Returns400()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.ResetPasswordAsync(It.IsAny<string>(), It.IsAny<Guid>(), "weak", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResetPasswordResult(false, null, null, ["too weak"]));

        var controller = new AuthController(auth.Object)
        {
            ControllerContext = BuildControllerContext("admin", "Admin")
        };

        var result = await controller.ResetPasswordAsync(Guid.NewGuid(), new ResetUserPasswordRequest { NewPassword = "weak" }, CancellationToken.None);
        Assert.IsType<ObjectResult>(result);
    }

    private static ControllerContext BuildControllerContext(string userName, string role)
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, userName),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role)
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
