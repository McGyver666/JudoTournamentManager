using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JudoTournamentManagement.Api.Tests;

/// <summary>
/// End-to-end integration tests for the club-scoring (Vereinswertung) endpoints (G-05f):
/// authorization, 404/200 behavior, response shape, and the provisional/final status label.
/// </summary>
[Trait("Category", "UnitTest")]
public sealed class ClubScoringIntegrationTests : IClassFixture<ClubScoringIntegrationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public ClubScoringIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAgeGroupClubScoring_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/tournaments/{Guid.NewGuid()}/club-scoring/age-groups");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetGlobalClubScoring_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/tournaments/{Guid.NewGuid()}/club-scoring/global");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAgeGroupClubScoring_UnknownTournament_Returns404()
    {
        using var client = _factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var response = await client.GetAsync($"/api/tournaments/{Guid.NewGuid()}/club-scoring/age-groups");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGlobalClubScoring_UnknownTournament_Returns404()
    {
        using var client = _factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var response = await client.GetAsync($"/api/tournaments/{Guid.NewGuid()}/club-scoring/global");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAgeGroupClubScoring_EmptyTournament_Returns200WithStableShape()
    {
        using var client = _factory.CreateClient();
        await AuthenticateAdminAsync(client);
        var tournament = await CreateTournamentAsync(client, "Leeres Turnier");

        var response = await client.GetAsync($"/api/tournaments/{tournament.Id}/club-scoring/age-groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AgeGroupClubScoringResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(tournament.Id, body!.TournamentId);
        Assert.NotNull(body.Items);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task GetGlobalClubScoring_EmptyTournament_Returns200WithProvisionalShape()
    {
        using var client = _factory.CreateClient();
        await AuthenticateAdminAsync(client);
        var tournament = await CreateTournamentAsync(client, "Leeres Turnier Global");

        var response = await client.GetAsync($"/api/tournaments/{tournament.Id}/club-scoring/global");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GlobalClubScoringResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(tournament.Id, body!.TournamentId);
        // No planned fights yet: status must be provisional and structure stable.
        Assert.Equal("Provisional", body.Status);
        Assert.Equal(0, body.PlannedFights);
        Assert.Equal(0, body.CompletedFights);
        Assert.NotNull(body.Clubs);
        Assert.Empty(body.Clubs);
    }

    [Fact]
    public async Task GetClubScoring_AllFightsCompleted_ReportsFinalStatusAndScoredClub()
    {
        using var client = _factory.CreateClient();
        await AuthenticateAdminAsync(client);
        var tournament = await CreateTournamentAsync(client, "Vollständiges Turnier");
        var setup = await SeedTwoAthleteCategoryAsync(client, tournament.Id);
        await CompleteSingleFightAsync(client, tournament.Id, setup, winnerIsAthleteA: true);

        // Age-group block must be Final with the winning club scoring base points.
        var ageResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/club-scoring/age-groups");
        Assert.Equal(HttpStatusCode.OK, ageResponse.StatusCode);
        var age = await ageResponse.Content.ReadFromJsonAsync<AgeGroupClubScoringResponse>(JsonOptions);
        Assert.NotNull(age);
        var item = Assert.Single(age!.Items);
        Assert.Equal("U18", item.AgeGroup);
        Assert.Equal("Final", item.Status);
        Assert.Equal(item.PlannedFights, item.CompletedFights);
        Assert.True(item.PlannedFights > 0);

        // Winner's club: 1st place (7 base points), one win, one fight, rank 1.
        var winner = Assert.Single(item.Clubs, c => c.ClubId == setup.WinnerClubId);
        Assert.Equal(1, winner.Rank);
        Assert.Equal(1, winner.FirstPlaces);
        Assert.Equal(7, winner.BasePoints);
        Assert.Equal(1, winner.Wins);
        Assert.Equal(1, winner.Fights);

        // Loser's club: 2nd place (5 base points), no wins, one fight, rank 2.
        var loser = Assert.Single(item.Clubs, c => c.ClubId == setup.LoserClubId);
        Assert.Equal(2, loser.Rank);
        Assert.Equal(1, loser.SecondPlaces);
        Assert.Equal(5, loser.BasePoints);
        Assert.Equal(0, loser.Wins);
        Assert.Equal(1, loser.Fights);

        // Global block must be Final too.
        var globalResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/club-scoring/global");
        Assert.Equal(HttpStatusCode.OK, globalResponse.StatusCode);
        var global = await globalResponse.Content.ReadFromJsonAsync<GlobalClubScoringResponse>(JsonOptions);
        Assert.NotNull(global);
        Assert.Equal("Final", global!.Status);
        Assert.Equal(global.PlannedFights, global.CompletedFights);
        Assert.True(global.PlannedFights > 0);
        Assert.Contains(global.Clubs, c => c.ClubId == setup.WinnerClubId && c.BasePoints == 7);
    }

    [Fact]
    public async Task GetClubScoring_UnfinishedTournament_ReportsProvisionalStatus()
    {
        using var client = _factory.CreateClient();
        await AuthenticateAdminAsync(client);
        var tournament = await CreateTournamentAsync(client, "Laufendes Turnier");
        var setup = await SeedTwoAthleteCategoryAsync(client, tournament.Id);
        // Draw generated (fights planned) but no result confirmed yet.

        var ageResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/club-scoring/age-groups");
        var age = await ageResponse.Content.ReadFromJsonAsync<AgeGroupClubScoringResponse>(JsonOptions);
        Assert.NotNull(age);
        var item = Assert.Single(age!.Items);
        Assert.Equal("Provisional", item.Status);
        Assert.True(item.PlannedFights > 0);
        Assert.Equal(0, item.CompletedFights);

        var globalResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/club-scoring/global");
        var global = await globalResponse.Content.ReadFromJsonAsync<GlobalClubScoringResponse>(JsonOptions);
        Assert.NotNull(global);
        Assert.Equal("Provisional", global!.Status);
        Assert.True(global.PlannedFights > 0);
        Assert.Equal(0, global.CompletedFights);
    }

    [Fact]
    public async Task GetClubScoring_WithDisplayRole_Returns200()
    {
        using var client = _factory.CreateClient();
        await AuthenticateAdminAsync(client);
        var tournament = await CreateTournamentAsync(client, "Anzeige Turnier");

        // Read-only club scoring must be reachable by the display role.
        await CreateUserAsync(client, "display-clubs", "Display");
        client.DefaultRequestHeaders.Authorization = null;
        var displayToken = await LoginAsync(client, "display-clubs", "Display!1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", displayToken);

        var ageResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/club-scoring/age-groups");
        var globalResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/club-scoring/global");

        Assert.Equal(HttpStatusCode.OK, ageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, globalResponse.StatusCode);
    }

    private static async Task<CategorySetup> SeedTwoAthleteCategoryAsync(HttpClient client, Guid tournamentId)
    {
        var category = await PostAsync<Category>(client, $"/api/tournaments/{tournamentId}/categories",
            new CreateCategoryRequest
            {
                Name = "U18 M -66",
                AgeGroup = "U18",
                Gender = Gender.Male,
                WeightClassKg = 66m,
                MinBirthYear = 2008,
                MaxBirthYear = 2012,
                MatchDurationSeconds = 300,
            });

        var clubA = await PostAsync<Club>(client, $"/api/tournaments/{tournamentId}/clubs",
            new CreateClubRequest { Name = "SC Alpha" });
        var clubB = await PostAsync<Club>(client, $"/api/tournaments/{tournamentId}/clubs",
            new CreateClubRequest { Name = "SC Beta" });

        var athleteA = await PostAsync<Athlete>(client, $"/api/tournaments/{tournamentId}/athletes",
            new CreateAthleteRequest
            {
                FirstName = "Max",
                LastName = "Mustermann",
                BirthYear = 2010,
                Gender = Gender.Male,
                ClubId = clubA.Id,
                Grade = 1,
            });
        var athleteB = await PostAsync<Athlete>(client, $"/api/tournaments/{tournamentId}/athletes",
            new CreateAthleteRequest
            {
                FirstName = "Erik",
                LastName = "Muster",
                BirthYear = 2011,
                Gender = Gender.Male,
                ClubId = clubB.Id,
                Grade = 1,
            });

        var regA = await PostAsync<Registration>(client, $"/api/tournaments/{tournamentId}/registrations",
            new CreateRegistrationRequest { AthleteId = athleteA.Id, WeightKg = 65m });
        var regB = await PostAsync<Registration>(client, $"/api/tournaments/{tournamentId}/registrations",
            new CreateRegistrationRequest { AthleteId = athleteB.Id, WeightKg = 58m });

        await AssertOkAsync(client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/registrations/{regA.Id}/category",
            new AssignCategoryRequest { CategoryId = category.Id }));
        await AssertOkAsync(client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/registrations/{regB.Id}/category",
            new AssignCategoryRequest { CategoryId = category.Id }));

        var drawResponse = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/categories/{category.Id}/draw",
            new GenerateDrawRequest { Format = BracketFormat.SingleElimination });
        Assert.Equal(HttpStatusCode.Created, drawResponse.StatusCode);
        var fights = await drawResponse.Content.ReadFromJsonAsync<List<Fight>>(JsonOptions);
        Assert.NotNull(fights);
        var realFight = Assert.Single(fights!.Where(f => !f.IsBye));

        return new CategorySetup(category.Id, clubA.Id, clubB.Id, athleteA.Id, athleteB.Id, realFight.Id);
    }

    private static async Task CompleteSingleFightAsync(
        HttpClient client,
        Guid tournamentId,
        CategorySetup setup,
        bool winnerIsAthleteA)
    {
        await AssertNoContentAsync(client.PostAsync(
            $"/api/tournaments/{tournamentId}/fights/{setup.FightId}/start", null));

        var fightsResponse = await client.GetAsync(
            $"/api/tournaments/{tournamentId}/categories/{setup.CategoryId}/fights");
        var fightList = await fightsResponse.Content.ReadFromJsonAsync<List<Fight>>(JsonOptions);
        var fight = Assert.Single(fightList!.Where(f => f.Id == setup.FightId));

        var winnerId = winnerIsAthleteA ? setup.AthleteAId : setup.AthleteBId;
        var side = fight.WhiteAthleteId == winnerId ? "White" : "Blue";

        await AssertNoContentAsync(client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/fights/{setup.FightId}/score/adjust",
            new AdjustScoreRequest { Side = side, ScoreType = ScoreType.Ippon, Delta = 1 }));

        await AssertNoContentAsync(client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/fights/{setup.FightId}/result",
            new ConfirmResultRequest { WinnerId = winnerId }));
    }

    private static async Task AuthenticateAdminAsync(HttpClient client)
    {
        var bootstrap = await client.PostAsJsonAsync("/api/auth/bootstrap-admin", new BootstrapAdminRequest
        {
            UserName = "admin",
            Password = "Admin!123456",
        });
        Assert.True(
            bootstrap.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected bootstrap status: {bootstrap.StatusCode}");

        var token = await LoginAsync(client, "admin", "Admin!123456");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task CreateUserAsync(HttpClient client, string userName, string role)
    {
        var response = await client.PostAsJsonAsync("/api/auth/users", new CreateUserRequest
        {
            UserName = userName,
            Role = role,
            Password = role + "!1234",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload!.AccessToken;
    }

    private static async Task<Tournament> CreateTournamentAsync(HttpClient client, string name)
    {
        return await PostAsync<Tournament>(client, "/api/tournaments", new CreateTournamentRequest
        {
            Name = name,
            Date = new DateOnly(2026, 8, 15),
            Venue = "Halle 1",
            Organizer = "JC Test",
        });
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"POST {url} returned {(int)response.StatusCode}: {payload}");
        var result = JsonSerializer.Deserialize<T>(payload, JsonOptions);
        Assert.NotNull(result);
        return result!;
    }

    private static async Task AssertOkAsync(Task<HttpResponseMessage> call)
    {
        var response = await call;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task AssertNoContentAsync(Task<HttpResponseMessage> call)
    {
        var response = await call;
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private sealed record CategorySetup(
        Guid CategoryId,
        Guid WinnerClubId,
        Guid LoserClubId,
        Guid AthleteAId,
        Guid AthleteBId,
        Guid FightId);

    public sealed class ApiFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly string _dbDirectory = Path.Combine(
            Path.GetTempPath(),
            "JudoTournamentTests_ClubScoring",
            Guid.NewGuid().ToString("N"));
        private readonly string _dbPath;

        public ApiFactory()
        {
            Directory.CreateDirectory(_dbDirectory);
            _dbPath = Path.Combine(_dbDirectory, "club-scoring.db");
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
