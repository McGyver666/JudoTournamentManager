using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class GuestAccessIntegrationTests : IClassFixture<GuestAccessIntegrationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public GuestAccessIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GuestToken_CanReadPublicAthletes_ButNotOperatorEndpoints()
    {
        using var client = _factory.CreateClient();
        var adminToken = await BootstrapAndLoginAdminAsync(client);
        var tournamentId = await CreateTournamentAsync(client, adminToken, "Gastturnier");
        var guestToken = await EnableGuestShareAsync(client, adminToken, tournamentId);

        using var guestClient = _factory.CreateClient();
        guestClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var publicResponse = await guestClient.GetAsync($"/api/tournaments/{tournamentId}/public/athletes");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

        // A bare [Authorize] operator endpoint must reject the guest principal.
        var operatorResponse = await guestClient.GetAsync("/api/tournaments");
        Assert.Equal(HttpStatusCode.Forbidden, operatorResponse.StatusCode);
    }

    [Fact]
    public async Task GuestToken_CannotReadOtherTournament()
    {
        using var client = _factory.CreateClient();
        var adminToken = await BootstrapAndLoginAdminAsync(client);
        var tournamentA = await CreateTournamentAsync(client, adminToken, "Turnier A");
        var tournamentB = await CreateTournamentAsync(client, adminToken, "Turnier B");
        var guestTokenA = await EnableGuestShareAsync(client, adminToken, tournamentA);

        using var guestClient = _factory.CreateClient();
        guestClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestTokenA);

        var response = await guestClient.GetAsync($"/api/tournaments/{tournamentB}/public/athletes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GuestToken_AfterDisable_IsRejected()
    {
        using var client = _factory.CreateClient();
        var adminToken = await BootstrapAndLoginAdminAsync(client);
        var tournamentId = await CreateTournamentAsync(client, adminToken, "Deaktivierbar");
        var guestToken = await EnableGuestShareAsync(client, adminToken, tournamentId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var disableResponse = await client.PostAsync($"/api/tournaments/{tournamentId}/guest-share/disable", null);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        using var guestClient = _factory.CreateClient();
        guestClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var response = await guestClient.GetAsync($"/api/tournaments/{tournamentId}/public/athletes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> BootstrapAndLoginAdminAsync(HttpClient client)
    {
        var bootstrapResponse = await client.PostAsJsonAsync("/api/auth/bootstrap-admin", new BootstrapAdminRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });

        Assert.True(
            bootstrapResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected bootstrap status: {bootstrapResponse.StatusCode}");

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload!.AccessToken;
    }

    private static async Task<Guid> CreateTournamentAsync(HttpClient client, string adminToken, string name)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PostAsJsonAsync("/api/tournaments", new CreateTournamentRequest
        {
            Name = name,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Venue = "Halle",
            Organizer = "JV Test"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TournamentIdDto>();
        Assert.NotNull(created);
        return created!.Id;
    }

    private static async Task<string> EnableGuestShareAsync(HttpClient client, string adminToken, Guid tournamentId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/guest-share/enable",
            new GuestShareRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<GuestShareResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
        return payload.Token!;
    }

    private sealed record TournamentIdDto(Guid Id);

    public sealed class ApiFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "JudoTournamentTests_GuestAccess", Guid.NewGuid().ToString("N"));
        private readonly string _dbPath;

        public ApiFactory()
        {
            Directory.CreateDirectory(_dbDirectory);
            _dbPath = Path.Combine(_dbDirectory, "guest-access.db");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
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
}
