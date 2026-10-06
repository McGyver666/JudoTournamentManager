using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ShiaiManager.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class BackupRestoreIntegrationTests : IClassFixture<BackupRestoreIntegrationTests.ApiFactory>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public BackupRestoreIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BackupEndpoint_RequiresAdminRole_Returns403ForOperator()
    {
        using var client = _factory.CreateClient();
        var adminToken = await EnsureAdminAndGetTokenAsync(client);
        var operatorToken = await CreateUserAndGetTokenAsync(client, adminToken, "op_backup1", "Operator");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var response = await client.GetAsync($"/api/tournaments/{Guid.NewGuid()}/backup");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BackupEndpoint_Returns404_WhenTournamentNotFound()
    {
        using var client = _factory.CreateClient();
        var adminToken = await EnsureAdminAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync($"/api/tournaments/{Guid.NewGuid()}/backup");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BackupEndpoint_ReturnsTournamentJson_WhenTournamentExists()
    {
        using var client = _factory.CreateClient();
        var adminToken = await EnsureAdminAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createResponse = await client.PostAsJsonAsync("/api/tournaments", new CreateTournamentRequest
        {
            Name = "Backup-Testturnier",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Venue = "Testort",
            Organizer = "Testveranstalter"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var tournament = await createResponse.Content.ReadFromJsonAsync<Models.Tournament>();
        Assert.NotNull(tournament);

        var backupResponse = await client.GetAsync($"/api/tournaments/{tournament!.Id}/backup");
        Assert.Equal(HttpStatusCode.OK, backupResponse.StatusCode);
        Assert.Equal("application/json", backupResponse.Content.Headers.ContentType?.MediaType);

        var body = await backupResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"version\"", body);
        Assert.Contains("Backup-Testturnier", body);
    }

    [Fact]
    public async Task RestoreEndpoint_Creates201_WithValidMinimalBackup()
    {
        using var client = _factory.CreateClient();
        var adminToken = await EnsureAdminAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var now = DateTimeOffset.UtcNow;
        var newId = Guid.NewGuid();
        var backup = new TournamentBackup
        {
            Version = "1.0",
            ExportedAtUtc = now,
            Tournament = new TournamentRecord
            {
                Id = newId,
                Name = "Restored Turnier",
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Venue = "Testort",
                Organizer = "Testveranstalter",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            Clubs =
            [
                new ClubRecord
                {
                    Id = Guid.NewGuid(),
                    TournamentId = newId,
                    Name = "SC Restored",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                }
            ]
        };

        var restoreResponse = await client.PostAsJsonAsync("/api/tournaments/restore", backup);
        var body = await restoreResponse.Content.ReadAsStringAsync();
        Assert.True(
            restoreResponse.StatusCode == HttpStatusCode.Created,
            $"Expected 201 but got {(int)restoreResponse.StatusCode}: {body}");

        // Verify the restored tournament is accessible.
        var getResponse = await client.GetAsync($"/api/tournaments/{newId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var restored = await getResponse.Content.ReadFromJsonAsync<Models.Tournament>();
        Assert.NotNull(restored);
        Assert.Equal("Restored Turnier", restored!.Name);
    }

    [Fact]
    public async Task RestoreEndpoint_Returns409_WhenTournamentAlreadyExists()
    {
        using var client = _factory.CreateClient();
        var adminToken = await EnsureAdminAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createResponse = await client.PostAsJsonAsync("/api/tournaments", new CreateTournamentRequest
        {
            Name = "Conflict-Test",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Venue = "Ort",
            Organizer = "Org"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var existing = await createResponse.Content.ReadFromJsonAsync<Models.Tournament>();
        Assert.NotNull(existing);

        var now = DateTimeOffset.UtcNow;
        var backup = new TournamentBackup
        {
            Version = "1.0",
            ExportedAtUtc = now,
            Tournament = new TournamentRecord
            {
                Id = existing!.Id,
                Name = "Duplicate",
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Venue = "X",
                Organizer = "X",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        };

        var restoreResponse = await client.PostAsJsonAsync("/api/tournaments/restore", backup);
        Assert.Equal(HttpStatusCode.Conflict, restoreResponse.StatusCode);
    }

    [Fact]
    public async Task RestoreEndpoint_Returns400_ForUnsupportedVersion()
    {
        using var client = _factory.CreateClient();
        var adminToken = await EnsureAdminAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var now = DateTimeOffset.UtcNow;
        var backup = new TournamentBackup
        {
            Version = "9.9",
            ExportedAtUtc = now,
            Tournament = new TournamentRecord
            {
                Id = Guid.NewGuid(),
                Name = "Version test",
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Venue = "X",
                Organizer = "X",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        };

        var response = await client.PostAsJsonAsync("/api/tournaments/restore", backup);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BackupEndpoint_IncludesCategoryPresets()
    {
        using var client = _factory.CreateClient();
        var adminToken = await EnsureAdminAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var createResponse = await client.PostAsJsonAsync("/api/tournaments", new CreateTournamentRequest
        {
            Name = "Preset-Backup",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Venue = "Testort",
            Organizer = "Testveranstalter"
        });
        var tournament = await createResponse.Content.ReadFromJsonAsync<Models.Tournament>();

        var backup = await client.GetFromJsonAsync<TournamentBackup>($"/api/tournaments/{tournament!.Id}/backup");

        Assert.NotNull(backup);
        Assert.Contains(backup!.CategoryPresets, preset => preset.AgeGroup == "U9");
        Assert.Contains(backup.CategoryPresets, preset => preset.AgeGroup == "Männer" && preset.MinAgeYears == 17);
    }

    [Fact]
    public async Task RestoreEndpoint_RestoresPresetsAndStartAgeGroup()
    {
        using var client = _factory.CreateClient();
        var adminToken = await EnsureAdminAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var now = DateTimeOffset.UtcNow;
        var tournamentId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var backup = new TournamentBackup
        {
            Version = "1.0",
            ExportedAtUtc = now,
            Tournament = new TournamentRecord
            {
                Id = tournamentId,
                Name = "Restored Presets",
                Date = new DateOnly(2026, 10, 5),
                Venue = "Testort",
                Organizer = "Testveranstalter",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            CategoryPresets =
            [
                new CategoryPresetRecord
                {
                    Id = Guid.NewGuid(), TournamentId = tournamentId, AgeGroup = "Offen", Gender = "Female",
                    MaxAgeYears = 12, MinAgeYears = 6, DefaultMatchDurationSeconds = 120,
                    WeightClassLimitsJson = "[]", SortOrder = 0
                }
            ],
            Clubs = [new ClubRecord { Id = clubId, TournamentId = tournamentId, Name = "SC Restored", CreatedAtUtc = now, UpdatedAtUtc = now }],
            Athletes =
            [
                new AthleteRecord
                {
                    Id = athleteId, TournamentId = tournamentId, ClubId = clubId, FirstName = "Lina", LastName = "Test",
                    BirthYear = 2016, Gender = "Female", WeightKg = 29m, CreatedAtUtc = now, UpdatedAtUtc = now
                }
            ],
            Registrations =
            [
                new RegistrationRecord
                {
                    Id = Guid.NewGuid(), TournamentId = tournamentId, AthleteId = athleteId, StartAgeGroup = "Offen",
                    LicenseConfirmed = true, CreatedAtUtc = now
                }
            ]
        };

        var restoreResponse = await client.PostAsJsonAsync("/api/tournaments/restore", backup);
        Assert.Equal(HttpStatusCode.Created, restoreResponse.StatusCode);

        var presets = await client.GetFromJsonAsync<List<CategoryPresetResponse>>($"/api/tournaments/{tournamentId}/category-presets", JsonOptions);
        Assert.Equal("Offen", Assert.Single(presets!).AgeGroup);
        var registrations = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/tournaments/{tournamentId}/registrations");
        Assert.Equal("Offen", Assert.Single(registrations.EnumerateArray()).GetProperty("startAgeGroup").GetString());
    }

    [Fact]
    public async Task RestoreEndpoint_SeedsDefaultPresets_ForBackupWithoutPresets()
    {
        using var client = _factory.CreateClient();
        var adminToken = await EnsureAdminAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var now = DateTimeOffset.UtcNow;
        var tournamentId = Guid.NewGuid();
        // Older backups have no "categoryPresets" property at all.
        var legacyJson = $$"""
            {
              "version": "1.0",
              "exportedAtUtc": "{{now:O}}",
              "tournament": {
                "id": "{{tournamentId}}",
                "name": "Legacy Backup",
                "date": "2026-10-05",
                "venue": "Testort",
                "organizer": "Testveranstalter",
                "createdAtUtc": "{{now:O}}",
                "updatedAtUtc": "{{now:O}}"
              }
            }
            """;

        var restoreResponse = await client.PostAsync(
            "/api/tournaments/restore",
            new StringContent(legacyJson, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, restoreResponse.StatusCode);

        var presets = await client.GetFromJsonAsync<List<CategoryPresetResponse>>($"/api/tournaments/{tournamentId}/category-presets", JsonOptions);
        Assert.Contains(presets!, preset => preset.AgeGroup == "U9");
        Assert.Contains(presets!, preset => preset.AgeGroup == "Männer");
    }

    private static async Task<string> EnsureAdminAndGetTokenAsync(HttpClient client)
    {
        var bootstrapResponse = await client.PostAsJsonAsync("/api/auth/bootstrap-admin", new BootstrapAdminRequest
        {
            UserName = "admin",
            Password = "Admin!123456"
        });
        Assert.True(
            bootstrapResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected bootstrap status: {bootstrapResponse.StatusCode}");

        return await LoginAndGetTokenAsync(client, "admin", "Admin!123456");
    }

    private static async Task<string> CreateUserAndGetTokenAsync(
        HttpClient client, string adminToken, string userName, string role)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PostAsJsonAsync("/api/auth/users", new CreateUserRequest
        {
            UserName = userName,
            Role = role,
            Password = role + "!1234"
        });
        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"User creation failed: {response.StatusCode}");
        client.DefaultRequestHeaders.Authorization = null;

        return await LoginAndGetTokenAsync(client, userName, role + "!1234");
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
        return payload!.AccessToken;
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "JudoTournamentTests_Backup", Guid.NewGuid().ToString("N"));
        private readonly string _dbPath;

        public ApiFactory()
        {
            Directory.CreateDirectory(_dbDirectory);
            _dbPath = Path.Combine(_dbDirectory, "backup_test.db");
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