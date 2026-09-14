using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Tests;

/// <summary>
/// Exercises the public API for team-matchday configuration.
/// </summary>
[Trait("Category", "UnitTest")]
public sealed class TeamMatchdaysApiTests : IClassFixture<TournamentFlowSmokeTests.ApiFactory>
{
    private readonly TournamentFlowSmokeTests.ApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Initializes the test class.</summary>
    public TeamMatchdaysApiTests(TournamentFlowSmokeTests.ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TeamMatchdayConfiguration_AuthorizedAdminCanAddTeamAndSetWeightClassOrder()
    {
        // Arrange
        using var client = _factory.CreateClient();
        await AuthenticateAsync(client);
        var tournament = await CreateAsync<Tournament>(client, "/api/tournaments", new CreateTournamentRequest
        {
            Name = "Landesliga Kampftag",
            Date = new DateOnly(2026, 9, 19),
            Venue = "Sporthalle Nord",
            Organizer = "JC Nord",
            CompetitionMode = CompetitionMode.TeamMatchday,
            TeamMatchdayProfile = TeamMatchdayProfile.SeniorMen
        });
        var club = await CreateAsync<Club>(client, $"/api/tournaments/{tournament.Id}/clubs", new CreateClubRequest
        {
            Name = "JC Nord"
        });
        var athlete = await CreateAsync<Athlete>(client, $"/api/tournaments/{tournament.Id}/athletes", new CreateAthleteRequest
        {
            FirstName = "Max",
            LastName = "Mustermann",
            BirthYear = 1998,
            Gender = Gender.Male,
            ClubId = club.Id,
            Grade = 1
        });

        // Act
        var teamResponse = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/team-matchday/teams",
            new { clubId = club.Id, name = "JC Nord I" });
        var orderResponse = await client.PutAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/team-matchday/weight-class-order",
            new { order = new[] { 4, 2, 0, 3, 1 } });
        var configurationResponse = await client.GetAsync($"/api/tournaments/{tournament.Id}/team-matchday");

        // Assert
        Assert.Equal(HttpStatusCode.Created, teamResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, configurationResponse.StatusCode);
        var configuration = await configurationResponse.Content.ReadFromJsonAsync<TeamMatchday>(JsonOptions);
        Assert.NotNull(configuration);
        Assert.Single(configuration!.Teams);
        Assert.Equal(new[] { 4, 2, 0, 3, 1 }, configuration.WeightClassOrder);
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(result);
        return result!;
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        var bootstrap = await client.PostAsJsonAsync("/api/auth/bootstrap-admin", new BootstrapAdminRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });
        Assert.True(bootstrap.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var payload = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
    }
}