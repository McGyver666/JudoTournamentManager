using System.Text;
using Moq;
using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Xunit;

namespace ShiaiManager.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class ResultsCsvExportServiceTests
{
    [Fact]
    public async Task CreateAsync_ProjectsRankingsIntoDeterministicEscapedCsv()
    {
        var tournamentId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var firstAthleteId = Guid.NewGuid();
        var secondAthleteId = Guid.NewGuid();
        var tournament = CreateTournament(tournamentId, "Sommer \"Pokal\"; 2026", "Halle\nNord");
        var category = CreateCategory(categoryId, tournamentId, "U15; -73", Gender.Male, 73m);
        var firstAthlete = CreateAthlete(firstAthleteId, tournamentId, "Ömer\nJunior", "Ärger");
        var secondAthlete = CreateAthlete(secondAthleteId, tournamentId, "Anna", "Müller");

        var rankingService = new Mock<IRankingService>();
        rankingService
            .Setup(service => service.GetCategoryRankingsAsync(tournamentId, categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RankingEntry(2, secondAthleteId, "Müller, Anna", "Verein; Süd"),
                new RankingEntry(1, firstAthleteId, "Ärger, Ömer\nJunior", "Verein \"Nord\""),
            });

        var categoriesStore = new Mock<ICategoriesStore>();
        categoriesStore
            .Setup(store => store.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { category });

        var athletesStore = new Mock<IAthletesStore>();
        athletesStore
            .Setup(store => store.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { secondAthlete, firstAthlete });

        var service = new ResultsCsvExportService(
            rankingService.Object,
            categoriesStore.Object,
            athletesStore.Object);

        var result = await service.CreateAsync(tournament, CancellationToken.None);

        Assert.Equal("ergebnisse-Sommer _Pokal_; 2026.csv", result.FileName);
        Assert.Equal(Encoding.UTF8.GetPreamble(), result.Content[..3]);

        var csv = Encoding.UTF8.GetString(result.Content[3..]);
        var expected = string.Join("\r\n", new[]
        {
            "Turniername;Turnierdatum;Veranstaltungsort;Veranstalter;Vorname;Nachname;Verein;Altersklasse;Kategorie;Geschlecht;Gewichtsklasse;Platzierung;Ergebnisstatus",
            "\"Sommer \"\"Pokal\"\"; 2026\";2026-09-21;\"Halle\nNord\";Judo Verein;\"Ömer\nJunior\";Ärger;\"Verein \"\"Nord\"\"\";U15;\"U15; -73\";Männlich;-73 kg;1;Vorläufig",
            "\"Sommer \"\"Pokal\"\"; 2026\";2026-09-21;\"Halle\nNord\";Judo Verein;Anna;Müller;\"Verein; Süd\";U15;\"U15; -73\";Männlich;-73 kg;2;Vorläufig",
            string.Empty,
        });

        Assert.Equal(expected, csv);
    }

    [Fact]
    public async Task CreateAsync_WithNoRankings_ReturnsHeaderOnly()
    {
        var tournamentId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var tournament = CreateTournament(tournamentId, "Ohne Ergebnisse", "Halle");
        var category = CreateCategory(categoryId, tournamentId, "U11", Gender.Mixed, null);

        var rankingService = new Mock<IRankingService>();
        rankingService
            .Setup(service => service.GetCategoryRankingsAsync(tournamentId, categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RankingEntry>());

        var categoriesStore = new Mock<ICategoriesStore>();
        categoriesStore
            .Setup(store => store.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { category });

        var athletesStore = new Mock<IAthletesStore>();
        athletesStore
            .Setup(store => store.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Athlete>());

        var service = new ResultsCsvExportService(
            rankingService.Object,
            categoriesStore.Object,
            athletesStore.Object);

        var result = await service.CreateAsync(tournament, CancellationToken.None);

        var csv = Encoding.UTF8.GetString(result.Content[3..]);
        Assert.Equal(
            "Turniername;Turnierdatum;Veranstaltungsort;Veranstalter;Vorname;Nachname;Verein;Altersklasse;Kategorie;Geschlecht;Gewichtsklasse;Platzierung;Ergebnisstatus\r\n",
            csv);
    }

    [Fact]
    public async Task CreateAsync_ExportsEveryCategoryInStoreOrder()
    {
        var tournamentId = Guid.NewGuid();
        var firstCategoryId = Guid.NewGuid();
        var secondCategoryId = Guid.NewGuid();
        var firstAthleteId = Guid.NewGuid();
        var secondAthleteId = Guid.NewGuid();
        var tournament = CreateTournament(tournamentId, "Mehrere Kategorien", "Halle");
        var firstCategory = CreateCategory(firstCategoryId, tournamentId, "Kategorie A", Gender.Male, null);
        var secondCategory = CreateCategory(secondCategoryId, tournamentId, "Kategorie B", Gender.Female, null);
        var firstAthlete = CreateAthlete(firstAthleteId, tournamentId, "Anna", "Alpha");
        var secondAthlete = CreateAthlete(secondAthleteId, tournamentId, "Zoe", "Zeta");

        var rankingService = new Mock<IRankingService>();
        rankingService
            .Setup(service => service.GetCategoryRankingsAsync(tournamentId, firstCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RankingEntry(1, firstAthleteId, "Alpha, Anna", "Verein A") });
        rankingService
            .Setup(service => service.GetCategoryRankingsAsync(tournamentId, secondCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RankingEntry(1, secondAthleteId, "Zeta, Zoe", "Verein B") });

        var categoriesStore = new Mock<ICategoriesStore>();
        categoriesStore
            .Setup(store => store.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { firstCategory, secondCategory });

        var athletesStore = new Mock<IAthletesStore>();
        athletesStore
            .Setup(store => store.GetAllAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { secondAthlete, firstAthlete });

        var service = new ResultsCsvExportService(
            rankingService.Object,
            categoriesStore.Object,
            athletesStore.Object);

        var result = await service.CreateAsync(tournament, CancellationToken.None);

        var csv = Encoding.UTF8.GetString(result.Content[3..]);
        var rows = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Kategorie A", rows[1].Split(';')[8]);
        Assert.Equal("Kategorie B", rows[2].Split(';')[8]);
        rankingService.Verify(
            service => service.GetCategoryRankingsAsync(tournamentId, firstCategoryId, It.IsAny<CancellationToken>()),
            Times.Once);
        rankingService.Verify(
            service => service.GetCategoryRankingsAsync(tournamentId, secondCategoryId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Tournament CreateTournament(Guid id, string name, string venue)
    {
        return new Tournament(
            id,
            name,
            new DateOnly(2026, 9, 21),
            venue,
            "Judo Verein",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static Category CreateCategory(Guid id, Guid tournamentId, string name, Gender gender, decimal? weightClassKg)
    {
        return new Category(
            id,
            tournamentId,
            name,
            "U15",
            gender,
            weightClassKg,
            null,
            null,
            null,
            240,
            false,
            180,
            null,
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static Athlete CreateAthlete(Guid id, Guid tournamentId, string firstName, string lastName)
    {
        return new Athlete(
            id,
            tournamentId,
            Guid.NewGuid(),
            firstName,
            lastName,
            2012,
            Gender.Male,
            null,
            null,
            1,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}