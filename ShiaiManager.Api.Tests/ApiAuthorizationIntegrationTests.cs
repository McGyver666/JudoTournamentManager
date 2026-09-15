using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ShiaiManager.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class ApiAuthorizationIntegrationTests : IClassFixture<ApiAuthorizationIntegrationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiAuthorizationIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateTournament_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tournaments", new CreateTournamentRequest
        {
            Name = "Sommerpokal",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Venue = "Essen",
            Organizer = "JV Essen"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTournament_WithDisplayRole_Returns403()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "display1", "Display");

        var displayToken = await LoginAndGetTokenAsync(client, "display1", "Display!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", displayToken);

        var response = await client.PostAsJsonAsync("/api/tournaments", new CreateTournamentRequest
        {
            Name = "Herbstturnier",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Venue = "Dortmund",
            Organizer = "JV Dortmund"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTournament_WithOperatorRole_Returns201()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "operator1", "Operator");

        var operatorToken = await LoginAndGetTokenAsync(client, "operator1", "Operator!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);

        var response = await client.PostAsJsonAsync("/api/tournaments", new CreateTournamentRequest
        {
            Name = "Fruehjahrscup",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Venue = "Koeln",
            Organizer = "JV Koeln"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CompetitionRole_CanBeCreatedAndCanReadTournaments()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "competition1", "Competition");

        var competitionToken = await LoginAndGetTokenAsync(client, "competition1", "Competition!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", competitionToken);

        var response = await client.GetAsync("/api/tournaments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CompetitionRole_CannotCreateTournament()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "competition2", "Competition");

        var competitionToken = await LoginAndGetTokenAsync(client, "competition2", "Competition!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", competitionToken);

        var response = await client.PostAsJsonAsync("/api/tournaments", new CreateTournamentRequest
        {
            Name = "Nicht erlaubt",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Venue = "Essen",
            Organizer = "JV Essen"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CompetitionRole_CanReachCompetitionReadAndLiveOperationEndpoints()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "competition-reads", "Competition");

        var token = await LoginAndGetTokenAsync(client, "competition-reads", "Competition!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var tournamentId = Guid.NewGuid();
        var fightId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var tatamiId = Guid.NewGuid();

        var requests = new (HttpMethod Method, string Uri, HttpContent? Content)[]
        {
            (HttpMethod.Get, $"/api/tournaments/{tournamentId}/completed-fights", null),
            (HttpMethod.Get, $"/api/tournaments/{tournamentId}/medal-table", null),
            (HttpMethod.Get, $"/api/tournaments/{tournamentId}/categories/{categoryId}/rankings", null),
            (HttpMethod.Get, $"/api/tournaments/{tournamentId}/tatamis/{tatamiId}/queue", null),
            (HttpMethod.Get, $"/api/tournaments/{tournamentId}/team-matchday/encounters/{Guid.NewGuid()}/lineups/1", null),
            (HttpMethod.Post, $"/api/tournaments/{tournamentId}/fights/{fightId}/queue-move", JsonContent.Create(new { Direction = "Up" })),
            (HttpMethod.Post, $"/api/tournaments/{tournamentId}/fights/{fightId}/start", null),
            (HttpMethod.Post, $"/api/tournaments/{tournamentId}/fights/{fightId}/result", JsonContent.Create(new { WinnerId = Guid.NewGuid() })),
        };

        foreach (var request in requests)
        {
            using var message = new HttpRequestMessage(request.Method, request.Uri) { Content = request.Content };
            using var response = await client.SendAsync(message);

            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
                $"Competition request {request.Method} {request.Uri} was rejected with {response.StatusCode}.");
        }
    }

    [Fact]
    public async Task CompetitionRole_CannotUseManagementOrAdministrativeEndpoints()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "competition-denied", "Competition");

        var token = await LoginAndGetTokenAsync(client, "competition-denied", "Competition!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var tournamentId = Guid.NewGuid();

        var requests = new (HttpMethod Method, string Uri, HttpContent? Content)[]
        {
            (HttpMethod.Put, $"/api/tournaments/{tournamentId}", JsonContent.Create(new { Name = "Nicht erlaubt" })),
            (HttpMethod.Post, $"/api/tournaments/{tournamentId}/categories", JsonContent.Create(new { Name = "Nicht erlaubt" })),
            (HttpMethod.Post, $"/api/tournaments/{tournamentId}/team-matchday/teams", JsonContent.Create(new { })),
            (HttpMethod.Get, $"/api/tournaments/{tournamentId}/audit-log", null),
            (HttpMethod.Get, $"/api/tournaments/{tournamentId}/backup", null),
            (HttpMethod.Get, $"/api/tournaments/{tournamentId}/guest-share", null),
            (HttpMethod.Post, $"/api/tournaments/{tournamentId}/completed-fights/{Guid.NewGuid()}/edit-result", JsonContent.Create(new { })),
        };

        foreach (var request in requests)
        {
            using var message = new HttpRequestMessage(request.Method, request.Uri) { Content = request.Content };
            using var response = await client.SendAsync(message);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task DisplayRole_CannotOperateLiveFight()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "display-live", "Display");

        var token = await LoginAndGetTokenAsync(client, "display-live", "Display!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync(
            $"/api/tournaments/{Guid.NewGuid()}/fights/{Guid.NewGuid()}/start", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin", "admin-regression", "Admin!123456")]
    [InlineData("Operator", "operator-regression", "Operator!1234")]
    public async Task AdminAndOperator_CanReachCompletedResultCorrection(string role, string userName, string password)
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, userName, role);

        var token = await LoginAndGetTokenAsync(client, userName, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{Guid.NewGuid()}/completed-fights/{Guid.NewGuid()}/edit-result",
            new EditFightResultRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTournaments_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/tournaments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "Current!Pass123",
            NewPassword = "New!Pass123456"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithOperatorRole_PreservesCurrentSessionAndRevokesOtherSessions()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "password-operator", "Operator");

        var currentToken = await LoginAndGetTokenAsync(client, "password-operator", "Operator!1234");
        var otherToken = await LoginAndGetTokenAsync(client, "password-operator", "Operator!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "Operator!1234",
            NewPassword = "Changed!Pass123"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var currentSessionResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, currentSessionResponse.StatusCode);

        using var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var otherSessionResponse = await otherClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, otherSessionResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var oldPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserName = "password-operator",
            Password = "Operator!1234"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordResponse.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithDisplayRole_Returns204()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "password-display", "Display");

        var token = await LoginAndGetTokenAsync(client, "password-display", "Display!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "Display!1234",
            NewPassword = "Changed!Pass456"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithCompetitionRole_Returns204()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "password-competition", "Competition");

        var token = await LoginAndGetTokenAsync(client, "password-competition", "Competition!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "Competition!1234",
            NewPassword = "Changed!Pass789"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetServerTime_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/time");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetServerTime_WithToken_Returns200()
    {
        using var client = _factory.CreateClient();
        var bootstrapResponse = await client.PostAsJsonAsync("/api/auth/bootstrap-admin", new BootstrapAdminRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });

        Assert.True(
            bootstrapResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected bootstrap status: {bootstrapResponse.StatusCode}");

        var token = await LoginAndGetTokenAsync(client, "admin", "Admin!123456");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/time");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTournaments_WithDisplayRole_Returns200()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "display2", "Display");

        var displayToken = await LoginAndGetTokenAsync(client, "display2", "Display!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", displayToken);

        var response = await client.GetAsync("/api/tournaments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExportRegistrationsCsv_WithDisplayRole_Returns403()
    {
        using var client = _factory.CreateClient();
        await BootstrapAdminAndCreateUserAsync(client, "display3", "Display");

        var displayToken = await LoginAndGetTokenAsync(client, "display3", "Display!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", displayToken);

        var tournamentId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/tournaments/{tournamentId}/registrations/export");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTatamiQueue_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var tournamentId = Guid.NewGuid();
        var tatamiId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/tournaments/{tournamentId}/tatamis/{tatamiId}/queue");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMedalTable_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var tournamentId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/tournaments/{tournamentId}/medal-table");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HubNegotiate_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/hubs/tournament/negotiate?negotiateVersion=1", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HubNegotiate_WithToken_Returns200()
    {
        using var client = _factory.CreateClient();
        var bootstrapResponse = await client.PostAsJsonAsync("/api/auth/bootstrap-admin", new BootstrapAdminRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });

        Assert.True(
            bootstrapResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected bootstrap status: {bootstrapResponse.StatusCode}");

        var token = await LoginAndGetTokenAsync(client, "admin", "Admin!123456");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/hubs/tournament/negotiate?negotiateVersion=1", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthResponse_ContainsSecurityHeaders()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task AuthEndpoints_RateLimit_ExcessiveRequests_Returns429()
    {
        using var rateLimitFactory = new RateLimitFactory();
        using var client = rateLimitFactory.CreateClient();

        HttpStatusCode finalStatus = HttpStatusCode.OK;
        for (var i = 0; i < 105; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                UserName = "rate-limit-user",
                Password = "invalid"
            });
            finalStatus = response.StatusCode;

            if (finalStatus == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, finalStatus);
    }

    private static async Task BootstrapAdminAndCreateUserAsync(HttpClient client, string userName, string role)
    {
        var bootstrapResponse = await client.PostAsJsonAsync("/api/auth/bootstrap-admin", new BootstrapAdminRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });

        Assert.True(
            bootstrapResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected bootstrap status: {bootstrapResponse.StatusCode}");

        var adminToken = await LoginAndGetTokenAsync(client, "admin", "Admin!123456");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createUserResponse = await client.PostAsJsonAsync("/api/auth/users", new CreateUserRequest
        {
            UserName = userName,
            Role = role,
            Password = role == "Admin" ? "Admin!123456" : role + "!1234"
        });

        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string userName, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));

        return payload.AccessToken;
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "ShiaiManagerTests", Guid.NewGuid().ToString("N"));
        private readonly string _dbPath;

        public ApiFactory()
        {
            Directory.CreateDirectory(_dbDirectory);
            _dbPath = Path.Combine(_dbDirectory, "integration.db");
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));
            });
        }

        public new void Dispose()
        {
            base.Dispose();
            try
            {
                if (Directory.Exists(_dbDirectory))
                {
                    Directory.Delete(_dbDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }

    private sealed class RateLimitFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "JudoTournamentTests_RateLimit", Guid.NewGuid().ToString("N"));
        private readonly string _dbPath;

        public RateLimitFactory()
        {
            Directory.CreateDirectory(_dbDirectory);
            _dbPath = Path.Combine(_dbDirectory, "ratelimit.db");
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("RateLimitValidation");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));
            });
        }

        public new void Dispose()
        {
            base.Dispose();
            try
            {
                if (Directory.Exists(_dbDirectory))
                {
                    Directory.Delete(_dbDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}
