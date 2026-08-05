using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JudoTournamentManagement.Api.Tests;

/// <summary>
/// Integration tests for the <c>UseForwardedHeaders</c> middleware (issue #12): verifies that an
/// <c>X-Forwarded-Proto: https</c> header from a trusted proxy yields an <c>https</c> request scheme
/// (so the guest-share public URL is HTTPS), while a request without the header keeps the local
/// <c>http</c> scheme.
/// </summary>
[Trait("Category", "UnitTest")]
public sealed class ForwardedHeadersIntegrationTests : IClassFixture<ForwardedHeadersIntegrationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public ForwardedHeadersIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GuestShareUrl_WhenXForwardedProtoIsHttps_UsesHttpsScheme()
    {
        using var client = _factory.CreateClient();

        var adminToken = await BootstrapAndLoginAdminAsync(client);
        var tournamentId = await CreateTournamentAsync(client, adminToken);

        // Simulate a request arriving via nginx with X-Forwarded-Proto: https.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "1.2.3.4");

        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/guest-share/enable",
            new GuestShareRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<GuestShareResponse>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.PublicUrl);
        Assert.StartsWith("https://", payload.PublicUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuestShareUrl_WhenNoForwardedHeader_UsesHttpScheme()
    {
        using var client = _factory.CreateClient();

        var adminToken = await BootstrapAndLoginAdminAsync(client);
        var tournamentId = await CreateTournamentAsync(client, adminToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        // No X-Forwarded-Proto header — scheme must remain http (local / LAN scenario).

        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/guest-share/enable",
            new GuestShareRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<GuestShareResponse>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.PublicUrl);
        Assert.StartsWith("http://", payload.PublicUrl, StringComparison.Ordinal);
    }

    private static async Task<string> BootstrapAndLoginAdminAsync(HttpClient client)
    {
        await client.PostAsJsonAsync("/api/auth/bootstrap-admin", new BootstrapAdminRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });

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

    private static async Task<Guid> CreateTournamentAsync(HttpClient client, string adminToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PostAsJsonAsync("/api/tournaments", new CreateTournamentRequest
        {
            Name = "Proxy-Test-Turnier",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Venue = "Halle",
            Organizer = "JV Test"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TournamentIdDto>();
        Assert.NotNull(created);
        return created!.Id;
    }

    private sealed record TournamentIdDto(Guid Id);

    public sealed class ApiFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "JudoTournamentTests_ForwardedHeaders", Guid.NewGuid().ToString("N"));
        private readonly string _dbPath;

        public ApiFactory()
        {
            Directory.CreateDirectory(_dbDirectory);
            _dbPath = Path.Combine(_dbDirectory, "forwarded-headers.db");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));

                // In-process test requests have no real remote IP, so relax the known-proxy
                // restriction to let X-Forwarded-Proto be processed in tests.
                services.Configure<ForwardedHeadersOptions>(opts =>
                {
                    opts.KnownProxies.Clear();
                    opts.KnownIPNetworks.Clear();
                });
            });
        }

        public new void Dispose()
        {
            base.Dispose();
            try
            {
                if (Directory.Exists(_dbDirectory))
                    Directory.Delete(_dbDirectory, true);
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}
