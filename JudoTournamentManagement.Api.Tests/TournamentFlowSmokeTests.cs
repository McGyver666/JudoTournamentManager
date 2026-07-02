using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JudoTournamentManagement.Api.Tests;

/// <summary>
/// End-to-end smoke test covering the full tournament flow:
/// setup -> clubs/athletes -> registration -> draw -> fight operations -> rankings.
/// Verifies I-03: "Automated tests for registration, draw generation, result progression, role authorization."
/// </summary>
[Trait("Category", "UnitTest")]
public sealed class TournamentFlowSmokeTests : IClassFixture<TournamentFlowSmokeTests.ApiFactory>
{
    private readonly ApiFactory _factory;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public TournamentFlowSmokeTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullTournamentFlow_SetupToRankings_CompletesSuccessfully()
    {
        using var client = _factory.CreateClient();

        // Step 1: Bootstrap admin and login.
        var bootstrapResponse = await client.PostAsJsonAsync("/api/auth/bootstrap-admin", new BootstrapAdminRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });
        Assert.True(
            bootstrapResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Bootstrap: {bootstrapResponse.StatusCode}");

        var adminToken = await LoginAsync(client, "admin", "Admin!123456");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Step 2: Create tournament.
        var tournament = await PostAndRead<Tournament>(client, "/api/tournaments", new CreateTournamentRequest
        {
            Name = "Smoke-Test Turnier",
            Date = new DateOnly(2026, 8, 15),
            Venue = "Halle 1",
            Organizer = "JC Smoke"
        });
        Assert.Equal("Smoke-Test Turnier", tournament.Name);

        // Step 3: Create tatami.
        var tatami = await PostAndRead<Tatami>(client, $"/api/tournaments/{tournament.Id}/tatamis",
            new CreateTatamiRequest { Name = "Tatami 1", DisplayOrder = 1 });

        // Step 4: Create category (U18 Männer -66 kg).
        var category = await PostAndRead<Category>(client, $"/api/tournaments/{tournament.Id}/categories",
            new CreateCategoryRequest
            {
                Name = "U18 M -66",
                AgeGroup = "U18",
                Gender = Gender.Male,
                WeightClassKg = 66m,
                MinBirthYear = 2008,
                MaxBirthYear = 2012,
                MatchDurationSeconds = 300
            });

        // Step 5: Create club and two athletes.
        var club = await PostAndRead<Club>(client, $"/api/tournaments/{tournament.Id}/clubs",
            new CreateClubRequest { Name = "SC Alpha" });

        var athleteA = await PostAndRead<Athlete>(client, $"/api/tournaments/{tournament.Id}/athletes",
            new CreateAthleteRequest
            {
                FirstName = "Max",
                LastName = "Mustermann",
                BirthYear = 2010,
                Gender = Gender.Male,
                ClubId = club.Id
            });
        var athleteB = await PostAndRead<Athlete>(client, $"/api/tournaments/{tournament.Id}/athletes",
            new CreateAthleteRequest
            {
                FirstName = "Erika",
                LastName = "Muster",
                BirthYear = 2011,
                Gender = Gender.Male,
                ClubId = club.Id
            });

        // Step 6: Register athletes and assign to category.
        var regA = await PostAndRead<Registration>(client, $"/api/tournaments/{tournament.Id}/registrations",
            new CreateRegistrationRequest { AthleteId = athleteA.Id, WeightKg = 65m });
        var regB = await PostAndRead<Registration>(client, $"/api/tournaments/{tournament.Id}/registrations",
            new CreateRegistrationRequest { AthleteId = athleteB.Id, WeightKg = 58m });

        var assignA = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/registrations/{regA.Id}/category",
            new AssignCategoryRequest { CategoryId = category.Id });
        Assert.Equal(HttpStatusCode.OK, assignA.StatusCode);

        var assignB = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/registrations/{regB.Id}/category",
            new AssignCategoryRequest { CategoryId = category.Id });
        Assert.Equal(HttpStatusCode.OK, assignB.StatusCode);

        // Step 7: Generate single-elimination draw.
        var drawResponse = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/categories/{category.Id}/draw",
            new GenerateDrawRequest { Format = BracketFormat.SingleElimination });
        Assert.Equal(HttpStatusCode.Created, drawResponse.StatusCode);
        var fights = await drawResponse.Content.ReadFromJsonAsync<List<Fight>>(JsonOptions);
        Assert.NotNull(fights);
        var realFights = fights!.Where(f => !f.IsBye).ToList();
        Assert.NotEmpty(realFights);

        // Step 8: Assign tatami and verify queue.
        var firstFight = realFights.First();
        var assignTatami = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/fights/{firstFight.Id}/assign-tatami",
            new AssignTatamiRequest { TatamiId = tatami.Id });
        Assert.Equal(HttpStatusCode.NoContent, assignTatami.StatusCode);

        var queueResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/tatamis/{tatami.Id}/queue");
        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);

        // Step 9: Start fight — category should lock after this.
        var startResponse = await client.PostAsync(
            $"/api/tournaments/{tournament.Id}/fights/{firstFight.Id}/start", null);
        Assert.Equal(HttpStatusCode.NoContent, startResponse.StatusCode);

        var catResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/categories/{category.Id}");
        var lockedCategory = await catResponse.Content.ReadFromJsonAsync<Category>(JsonOptions);
        Assert.NotNull(lockedCategory);
        Assert.True(lockedCategory!.IsLocked, "Category must be locked after first fight is started.");

        // Step 10: Adjust score (Ippon for White) and confirm result.
        // Determine which side athleteA is on.
        var fightDetails = await client.GetAsync(
            $"/api/tournaments/{tournament.Id}/categories/{category.Id}/fights");
        var fightList = await fightDetails.Content.ReadFromJsonAsync<List<Fight>>(JsonOptions);
        var currentFight = fightList!.First(f => f.Id == firstFight.Id);
        var side = currentFight.WhiteAthleteId == athleteA.Id ? "White" : "Blue";

        var scoreResponse = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/fights/{firstFight.Id}/score/adjust",
            new AdjustScoreRequest { Side = side, ScoreType = ScoreType.Ippon, Delta = 1 });
        Assert.Equal(HttpStatusCode.NoContent, scoreResponse.StatusCode);

        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/fights/{firstFight.Id}/result",
            new ConfirmResultRequest { WinnerId = athleteA.Id });
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        // Step 11: Rankings must show athleteA as first place.
        var rankingsResponse = await client.GetAsync(
            $"/api/tournaments/{tournament.Id}/categories/{category.Id}/rankings");
        Assert.Equal(HttpStatusCode.OK, rankingsResponse.StatusCode);
        var rankings = await rankingsResponse.Content.ReadFromJsonAsync<List<RankingEntry>>(JsonOptions);
        Assert.NotNull(rankings);
        Assert.NotEmpty(rankings!);
        Assert.Equal(athleteA.Id, rankings!.First().AthleteId);

        // Step 12: Medal table must include the club.
        var medalResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/medal-table");
        Assert.Equal(HttpStatusCode.OK, medalResponse.StatusCode);
        var medals = await medalResponse.Content.ReadFromJsonAsync<List<MedalEntry>>(JsonOptions);
        Assert.NotNull(medals);
        Assert.NotEmpty(medals!);
    }

    private static async Task<T> PostAndRead<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"POST {url} returned {(int)response.StatusCode}: {responseBody}");
        
        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) }
        };
        var result = System.Text.Json.JsonSerializer.Deserialize<T>(responseBody, jsonOptions);
        Assert.NotNull(result);
        return result!;
    }

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload!.AccessToken;
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "JudoTournamentTests_Smoke", Guid.NewGuid().ToString("N"));
        private readonly string _dbPath;

        public ApiFactory()
        {
            Directory.CreateDirectory(_dbDirectory);
            _dbPath = Path.Combine(_dbDirectory, "smoke_test.db");
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
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
                    Directory.Delete(_dbDirectory, true);
            }
            catch { }
        }
    }
}