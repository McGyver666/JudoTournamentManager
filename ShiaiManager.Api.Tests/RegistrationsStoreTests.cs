using ShiaiManager.Api.Data;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ShiaiManager.Api.Tests;

public sealed class RegistrationsStoreTests
{
    private static string CreateDatabasePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ShiaiManagerTests", Guid.NewGuid().ToString("N"));
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
    public async Task CreateAtWeighInAsync_WhenGradeAndLicenseChange_PersistsCorrectionsAndAuditEntry()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tournamentId, athleteId, _) = await SeedAsync(ctx);
        var athlete = await ctx.Athletes.SingleAsync(a => a.Id == athleteId);
        athlete.LicenseId = "OLD-123";
        await ctx.SaveChangesAsync();
        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);

        var created = await store.CreateAtWeighInAsync(
            tournamentId, athleteId, 65m, "NEW-456", 5, true, "operator", null, CancellationToken.None);

        await ctx.Entry(athlete).ReloadAsync();
        var audit = await ctx.AuditLogs.SingleAsync();
        Assert.NotNull(created);
        Assert.Equal(65m, athlete.WeightKg);
        Assert.Equal("NEW-456", athlete.LicenseId);
        Assert.Equal(5, athlete.Grade);
        Assert.Equal("AthleteCorrectedAtWeighIn", audit.Action);
        Assert.Equal("Athlete", audit.EntityType);
        Assert.Equal(athleteId, audit.EntityId);
        Assert.Equal("operator", audit.User);
        Assert.Equal("grade 1->5; licenseId changed", audit.Details);
        Assert.DoesNotContain("OLD-123", audit.Details);
        Assert.DoesNotContain("NEW-456", audit.Details);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task CreateAtWeighInAsync_WhenGradeIsNull_PreservesGradeAndDoesNotAuditUnchangedLicense()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tournamentId, athleteId, _) = await SeedAsync(ctx);
        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);

        await store.CreateAtWeighInAsync(
            tournamentId, athleteId, 65m, null, null, true, "operator", null, CancellationToken.None);

        var athlete = await ctx.Athletes.SingleAsync(a => a.Id == athleteId);
        Assert.Equal(1, athlete.Grade);
        Assert.Empty(await ctx.AuditLogs.ToListAsync());
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task UpdateStartAgeGroupAsync_StoresOverrideUnassignsCategoryAndAuditsChange()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tournamentId, athleteId, categoryId) = await SeedAsync(ctx);
        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        var registration = await store.CreateAsync(tournamentId, athleteId, 65m, true, CancellationToken.None);
        await store.AssignCategoryAsync(registration!.Id, categoryId, CancellationToken.None);

        var updated = await store.UpdateStartAgeGroupAsync(
            registration.Id,
            " U13 ",
            null,
            "operator",
            CancellationToken.None);

        var details = Assert.Single(await store.GetDetailedAsync(tournamentId, CancellationToken.None));
        var audit = Assert.Single(await ctx.AuditLogs.ToListAsync());
        Assert.Equal("U13", updated!.StartAgeGroup);
        Assert.Equal("U13", details.StartAgeGroup);
        Assert.Null(details.CategoryId);
        Assert.Equal("RegistrationStartAgeGroupChanged", audit.Action);
        Assert.Equal("Registration", audit.EntityType);
        Assert.Equal(registration.Id, audit.EntityId);
        Assert.Equal("operator", audit.User);
        Assert.Equal("natürlich->U13", audit.Details);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task SeedDefaultsAsync_IncludesWeightlessU9PresetsForBothGenders()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tournamentId, _, _) = await SeedAsync(ctx);
        var store = new SqliteCategoryPresetsStore(ctx);

        await store.SeedDefaultsAsync(tournamentId, 2026, CancellationToken.None);

        var u9Presets = (await store.GetAllAsync(tournamentId, CancellationToken.None))
            .Where(preset => preset.AgeGroup == "U9")
            .ToList();
        Assert.Equal(2, u9Presets.Count);
        Assert.All(u9Presets, preset =>
        {
            Assert.Equal(8, preset.MaxAgeYears);
            Assert.Equal(6, preset.MinAgeYears);
            Assert.Equal(120, preset.DefaultMatchDurationSeconds);
            Assert.Empty(preset.WeightClassLimitsKg);
        });
        Assert.Contains(u9Presets, preset => preset.Gender == Gender.Male);
        Assert.Contains(u9Presets, preset => preset.Gender == Gender.Female);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task SeedDefaultsAsync_AdultPresetsHaveMinimumAgeWithoutUpperLimit()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tournamentId, _, _) = await SeedAsync(ctx);
        var store = new SqliteCategoryPresetsStore(ctx);

        await store.SeedDefaultsAsync(tournamentId, 2026, CancellationToken.None);

        var presets = await store.GetAllAsync(tournamentId, CancellationToken.None);
        var adults = presets.Where(p => p.AgeGroup is "Männer" or "Frauen").ToList();
        Assert.Equal(2, adults.Count);
        Assert.All(adults, preset =>
        {
            Assert.Null(preset.MaxAgeYears);
            Assert.Equal(17, preset.MinAgeYears);
            Assert.Null(preset.MinBirthYear);
        });
        // Tournament date 2026-01-01: a 35-year-old is naturally in the adult class.
        Assert.Equal("Männer", AgeGroupResolver.GetNaturalAgeGroup(1991, Gender.Male, presets));
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Migration_FixAdultPresetAgeBounds_CorrectsOnlyUneditedAdultPresets()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.MigrateAsync("20261006120000_AddRegistrationStartAgeGroup");
        await ctx.Database.ExecuteSqlRawAsync(CategoryPresetsSchema.CreateTableSql);
        var tournamentId = Guid.NewGuid();
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CategoryPresets (Id, TournamentId, AgeGroup, Gender, MaxAgeYears, MinAgeYears, DefaultMatchDurationSeconds, WeightClassLimitsJson, SortOrder) VALUES ({Guid.NewGuid()}, {tournamentId}, {"Männer"}, {"Male"}, {17}, NULL, {240}, {"[]"}, {0})");
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CategoryPresets (Id, TournamentId, AgeGroup, Gender, MaxAgeYears, MinAgeYears, DefaultMatchDurationSeconds, WeightClassLimitsJson, SortOrder) VALUES ({Guid.NewGuid()}, {tournamentId}, {"Frauen"}, {"Female"}, {35}, {18}, {240}, {"[]"}, {1})");

        await ctx.Database.MigrateAsync();

        var presets = await ctx.CategoryPresets.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
        Assert.Null(presets[0].MaxAgeYears);
        Assert.Equal(17, presets[0].MinAgeYears);
        Assert.Equal(35, presets[1].MaxAgeYears);
        Assert.Equal(18, presets[1].MinAgeYears);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Migration_AddsNullableStartAgeGroupToSqliteRegistrations()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);

        await ctx.Database.MigrateAsync("20261002100000_MakeAthleteGradeOptional");
        var tournamentId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Registrations (Id, TournamentId, AthleteId, CategoryId, LicenseConfirmed, CreatedAtUtc) VALUES ({registrationId}, {tournamentId}, {athleteId}, NULL, {true}, {DateTimeOffset.UtcNow})");

        await ctx.Database.MigrateAsync();

        var appliedMigrations = await ctx.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20261006120000_AddRegistrationStartAgeGroup", appliedMigrations);
        var restoredRegistration = await ctx.Registrations.AsNoTracking().SingleAsync();
        Assert.Equal(registrationId, restoredRegistration.Id);
        Assert.Null(restoredRegistration.StartAgeGroup);

        await ctx.Database.OpenConnectionAsync();
        await using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA table_info('Registrations')";
        await using var reader = await command.ExecuteReaderAsync();
        var startAgeGroupFound = false;
        var startAgeGroupIsNullable = false;
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1) != "StartAgeGroup")
            {
                continue;
            }

            startAgeGroupFound = true;
            startAgeGroupIsNullable = reader.GetInt32(3) == 0;
            break;
        }

        Assert.True(startAgeGroupFound);
        Assert.True(startAgeGroupIsNullable);
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
        athlete.LicenseId = "legacy-license-test";
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
        Assert.Equal("qr-license-test", registration.LicenseNumber);
        Assert.Equal("legacy-license-test", athlete.LicenseId);
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
    public async Task AutoAssignAsync_UsesNaturalAndSelectedAgeGroupsBeforeWeightFit()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tournamentId, athleteId, seededCategoryId) = await SeedAsync(ctx);
        await new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance)
            .DeleteAsync(seededCategoryId, CancellationToken.None);

        var athlete = await ctx.Athletes.SingleAsync(x => x.Id == athleteId);
        athlete.BirthYear = 2016;
        athlete.WeightKg = 65m;
        await ctx.SaveChangesAsync();
        var higherStarter = await new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance)
            .CreateAsync(tournamentId, athlete.ClubId, "Lina", "Higher", 2016, Gender.Male, null, 65m, null, false,
                CancellationToken.None);

        ctx.CategoryPresets.AddRange(
            new CategoryPresetRecord
            {
                Id = Guid.NewGuid(), TournamentId = tournamentId, AgeGroup = "U13", Gender = "Male",
                MaxAgeYears = 12, MinAgeYears = 10, DefaultMatchDurationSeconds = 180,
                WeightClassLimitsJson = "[73,null]", SortOrder = 0
            },
            new CategoryPresetRecord
            {
                Id = Guid.NewGuid(), TournamentId = tournamentId, AgeGroup = "U11", Gender = "Male",
                MaxAgeYears = 10, MinAgeYears = 8, DefaultMatchDurationSeconds = 120,
                WeightClassLimitsJson = "[90,null]", SortOrder = 1
            });
        await ctx.SaveChangesAsync();

        var categoriesStore = new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance);
        var u13 = await categoriesStore.CreateAsync(
            tournamentId, "U13 M -73 kg", "U13", Gender.Male, 73m, 2014, 2016, null, 180, false, 180,
            CancellationToken.None);
        await categoriesStore.CreateAsync(
            tournamentId, "U11 M -90 kg", "U11", Gender.Male, 90m, 2016, 2018, null, 120, false, 180,
            CancellationToken.None);
        var registrationsStore = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        await registrationsStore.CreateAsync(tournamentId, athleteId, 65m, true, CancellationToken.None);
        var higherRegistration = await registrationsStore.CreateAsync(
            tournamentId, higherStarter!.Id, 65m, true, CancellationToken.None);
        var higherRecord = await ctx.Registrations.SingleAsync(x => x.Id == higherRegistration!.Id);
        higherRecord.StartAgeGroup = "U13";
        await ctx.SaveChangesAsync();

        var result = await registrationsStore.AutoAssignAsync(tournamentId, CancellationToken.None);
        var assigned = await registrationsStore.GetDetailedAsync(tournamentId, CancellationToken.None);
        var natural = assigned.Single(x => x.AthleteId == athleteId);
        var higher = assigned.Single(x => x.AthleteId == higherStarter.Id);

        Assert.Equal(2, result.AssignedCount);
        Assert.Equal("U11", natural.CategoryAgeGroup);
        Assert.Equal("U13", higher.CategoryAgeGroup);
        Assert.Equal(u13!.Id, higher.CategoryId);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task AutoAssignAsync_IgnoresCategoryBirthYearRangeAndReportsMissingAgeGroup()
    {
        var db = CreateDatabasePath();
        await using var ctx = CreateDbContext(db);
        await ctx.Database.EnsureCreatedAsync();
        var (tournamentId, athleteId, seededCategoryId) = await SeedAsync(ctx);
        await new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance)
            .DeleteAsync(seededCategoryId, CancellationToken.None);
        var athlete = await ctx.Athletes.SingleAsync(x => x.Id == athleteId);
        athlete.BirthYear = 2016;
        await ctx.SaveChangesAsync();
        var athleteWithoutPreset = await new SqliteAthletesStore(ctx, NullLogger<SqliteAthletesStore>.Instance)
            .CreateAsync(tournamentId, athlete.ClubId, "Old", "Timer", 1960, Gender.Male, null, 80m, null, false,
                CancellationToken.None);
        ctx.CategoryPresets.Add(new CategoryPresetRecord
        {
            Id = Guid.NewGuid(), TournamentId = tournamentId, AgeGroup = "U11", Gender = "Male",
            MaxAgeYears = 10, MinAgeYears = 8, DefaultMatchDurationSeconds = 120,
            WeightClassLimitsJson = "[40,null]", SortOrder = 0
        });
        await ctx.SaveChangesAsync();
        // Birth-year range 2017-2018 does not contain 2016; it is only a plausibility hint.
        var category = await new SqliteCategoriesStore(ctx, NullLogger<SqliteCategoriesStore>.Instance).CreateAsync(
            tournamentId, "U11 M -40 kg", "U11", Gender.Male, 40m, 2017, 2018, null, 120, false, 180,
            CancellationToken.None);
        var store = new SqliteRegistrationsStore(ctx, NullLogger<SqliteRegistrationsStore>.Instance);
        await store.CreateAsync(tournamentId, athleteId, 30m, true, CancellationToken.None);
        await store.CreateAsync(tournamentId, athleteWithoutPreset!.Id, 80m, true, CancellationToken.None);

        var result = await store.AutoAssignAsync(tournamentId, CancellationToken.None);

        Assert.Equal(1, result.AssignedCount);
        var details = await store.GetDetailedAsync(tournamentId, CancellationToken.None);
        Assert.Equal(category!.Id, details.Single(x => x.AthleteId == athleteId).CategoryId);
        var unassigned = Assert.Single(result.Unassigned);
        Assert.Equal(athleteWithoutPreset.Id, unassigned.AthleteId);
        Assert.Equal(AgeGroupMessages.NoPresetForBirthYear.Key, unassigned.ReasonKey);
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
            PassNumber = "qr-license-test",
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

