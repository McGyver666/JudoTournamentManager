using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JudoTournamentManagement.Api.Tests;

public sealed class RegistrationsStoreTests
{
    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "JudoTournamentManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "registrations.db");
    }

    private static AppDbContext CreateDbContext(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}").Options;
        return new AppDbContext(options);
    }

    private static async Task<(Guid TournamentId, Guid AthleteId, Guid CategoryId)> SeedAsync(AppDbContext ctx)
    {
        var mockPresets = new Mock<ICategoryPresetsStore>();
        mockPresets.Setup(p => p.SeedDefaultsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var tStore = new SqliteTournamentStore(ctx, NullLogger<SqliteTournamentStore>.Instance, mockPresets.Object);
        var t = await tStore.CreateAsync("T", new DateOnly(2026, 1, 1), "V", "O", CancellationToken.None);

        var clubStore = new SqliteClubsStore(ctx, NullLogger<SqliteClubsStore>.Instance);
        var club = await clubStore.CreateAsync(t.Id, "JC Test", null, null, null, CancellationToken.None);

        var athleteStore = new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance);
        var athlete = await athleteStore.CreateAsync(
            t.Id, club!.Id, "Max", "Mustermann", 2005, Gender.Male, null, null, 1, false, CancellationToken.None);

        var catStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var category = await catStore.CreateAsync(
            t.Id, "U18 Männer -73 kg", "U18", Gender.Male, 73m, null, null, null, 300, false, 180, CancellationToken.None);

        return (t.Id, athlete!.Id, category!.Id);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenValidInput_PersistsRegistration()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, aid, cid) = await SeedAsync(ctx);
        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);

        var created = await store.CreateAsync(tid, aid, 25.0m, null, false, CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(tid, created.TournamentId);
        Assert.Equal(aid, created.AthleteId);
        Assert.Null(created.CategoryId);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAsync_WhenAthleteAlreadyRegistered_ReturnsNull()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, aid, cid) = await SeedAsync(ctx);
        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);

        await store.CreateAsync(tid, aid, 25.0m, null, false, CancellationToken.None);
        var duplicate = await store.CreateAsync(tid, aid, 25.0m, null, false, CancellationToken.None);

        Assert.Null(duplicate);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateWithLicenseCheckAsync_PersistsQrLicenseNumberWithoutUpdatingAthleteLicense()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tournamentId, athleteId, _) = await SeedAsync(ctx);
        var athlete = await ctx.Athletes.SingleAsync(a => a.Id == athleteId);
        athlete.LicenseId = "legacy-license";
        await ctx.SaveChangesAsync();

        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var created = await store.CreateWithLicenseCheckAsync(
            tournamentId,
            athleteId,
            25.0m,
            true,
            "https://qr.dokume.net?d=l&s=token",
            null,
            new TestDokumePassParser(),
            new DateOnly(2026, 1, 1),
            "operator",
            CancellationToken.None);

        await ctx.Entry(athlete).ReloadAsync();
        var registration = await ctx.Registrations.SingleAsync(r => r.Id == created!.Id);

        Assert.NotNull(created);
        Assert.Equal("qr-license", registration.LicenseNumber);
        Assert.Equal("legacy-license", athlete.LicenseId);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetDetailedAsync_IncludesAthleteAndCategoryInfo()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, aid, cid) = await SeedAsync(ctx);
        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var reg = await store.CreateAsync(tid, aid, 25.0m, null, false, CancellationToken.None);
        await store.AssignCategoryAsync(reg!.Id, cid, CancellationToken.None);

        var details = await store.GetDetailedAsync(tid, CancellationToken.None);

        Assert.Single(details);
        var d = details[0];
        Assert.Equal("Mustermann", d.AthleteLastName);
        Assert.Equal("Max", d.AthleteFirstName);
        Assert.Equal(2005, d.AthleteBirthYear);
        Assert.Equal("JC Test", d.AthleteClubName);
        Assert.Equal("U18 Männer -73 kg", d.CategoryName);
        Assert.Equal("U18", d.CategoryAgeGroup);
        Assert.Equal(73m, d.CategoryWeightClassKg);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task GetDetailedAsync_ReturnsOnlyRegistrationsForTournament()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid1, aid1, cid1) = await SeedAsync(ctx);
        var (tid2, aid2, cid2) = await SeedAsync(ctx);

        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var r1 = await store.CreateAsync(tid1, aid1, 25.0m, null, false, CancellationToken.None);
        await store.AssignCategoryAsync(r1!.Id, cid1, CancellationToken.None);
        var r2 = await store.CreateAsync(tid2, aid2, 25.0m, null, false, CancellationToken.None);
        await store.AssignCategoryAsync(r2!.Id, cid2, CancellationToken.None);

        var result = await store.GetDetailedAsync(tid1, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(tid1, result[0].TournamentId);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenRegistrationExists_RemovesRegistration()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tid, aid, cid) = await SeedAsync(ctx);
        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var created = await store.CreateAsync(tid, aid, 25.0m, null, false, CancellationToken.None);
        await store.AssignCategoryAsync(created!.Id, cid, CancellationToken.None);

        var deleted = await store.DeleteAsync(created!.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await store.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task DeleteAsync_WhenRegistrationDoesNotExist_ReturnsFalse()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);

        Assert.False(await store.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private sealed class TestDokumePassParser : IDokumePassParser
    {
        public DokumePassCheckResult? ParseQrUrl(string? qrUrl) => new()
        {
            PassNumber = "qr-license",
            ExpiryDate = new DateOnly(2027, 1, 1)
        };

        public DokumePassValidationResult ValidatePass(
            DokumePassCheckResult parsed,
            DateOnly tournamentDate,
            string athleteFirstName,
            string athleteLastName,
            int athleteBirthYear) => new()
            {
                IsValid = true
            };
    }
}
