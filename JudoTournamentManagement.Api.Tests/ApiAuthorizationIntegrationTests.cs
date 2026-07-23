using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JudoTournamentManagement.Api.Tests;

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
    public async Task GetTournaments_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/tournaments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
            Password = role + "!1234"
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
        private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
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
