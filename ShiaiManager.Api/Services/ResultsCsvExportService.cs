using System.Globalization;
using System.Text;
using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Projects the existing category ranking results into the certificate CSV contract.
/// </summary>
public sealed class ResultsCsvExportService : IResultsCsvExportService
{
    private const string ProvisionalStatus = "Vorläufig";
    private const string InvalidFileNameCharacters = "<>:\"/\\|?*";

    private readonly IRankingService _rankingService;
    private readonly ICategoriesStore _categoriesStore;
    private readonly IAthletesStore _athletesStore;

    /// <summary>Initializes a new result CSV export service.</summary>
    public ResultsCsvExportService(
        IRankingService rankingService,
        ICategoriesStore categoriesStore,
        IAthletesStore athletesStore)
    {
        ArgumentNullException.ThrowIfNull(rankingService);
        ArgumentNullException.ThrowIfNull(categoriesStore);
        ArgumentNullException.ThrowIfNull(athletesStore);
        _rankingService = rankingService;
        _categoriesStore = categoriesStore;
        _athletesStore = athletesStore;
    }

    /// <inheritdoc />
    public async Task<ResultsCsvExport> CreateAsync(
        Tournament tournament,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tournament);

        var categories = await _categoriesStore.GetAllAsync(tournament.Id, cancellationToken);
        var athletes = await _athletesStore.GetAllAsync(tournament.Id, cancellationToken);
        var athletesById = athletes.ToDictionary(athlete => athlete.Id);
        var builder = new StringBuilder();

        AppendRow(builder,
            "Turniername",
            "Turnierdatum",
            "Veranstaltungsort",
            "Veranstalter",
            "Vorname",
            "Nachname",
            "Verein",
            "Altersklasse",
            "Kategorie",
            "Geschlecht",
            "Gewichtsklasse",
            "Platzierung",
            "Ergebnisstatus");

        foreach (var category in categories)
        {
            var rankings = await _rankingService.GetCategoryRankingsAsync(
                tournament.Id,
                category.Id,
                cancellationToken);

            foreach (var ranking in rankings
                         .OrderBy(entry => entry.Place)
                         .ThenBy(entry => entry.AthleteName, StringComparer.Ordinal)
                         .ThenBy(entry => entry.AthleteId))
            {
                var names = GetAthleteNames(ranking, athletesById);
                AppendRow(builder,
                    tournament.Name,
                    tournament.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    tournament.Venue,
                    tournament.Organizer,
                    names.FirstName,
                    names.LastName,
                    ranking.ClubName,
                    category.AgeGroup,
                    category.Name,
                    GenderLabel(category.Gender),
                    WeightClassLabel(category.WeightClassKg),
                    ranking.Place.ToString(CultureInfo.InvariantCulture),
                    ProvisionalStatus);
            }
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var content = encoding.GetPreamble()
            .Concat(encoding.GetBytes(builder.ToString()))
            .ToArray();

        return new ResultsCsvExport(content, BuildFileName(tournament.Name));
    }

    private static (string FirstName, string LastName) GetAthleteNames(
        RankingEntry ranking,
        IReadOnlyDictionary<Guid, Athlete> athletesById)
    {
        if (athletesById.TryGetValue(ranking.AthleteId, out var athlete))
        {
            return (athlete.FirstName, athlete.LastName);
        }

        var separator = ranking.AthleteName.IndexOf(',');
        return separator < 0
            ? (ranking.AthleteName, string.Empty)
            : (ranking.AthleteName[(separator + 1)..].Trim(), ranking.AthleteName[..separator].Trim());
    }

    private static string GenderLabel(Gender gender)
    {
        return gender switch
        {
            Gender.Male => "Männlich",
            Gender.Female => "Weiblich",
            Gender.Mixed => "Gemischt",
            _ => string.Empty,
        };
    }

    private static string WeightClassLabel(decimal? weightClassKg)
    {
        return weightClassKg.HasValue
            ? $"-{weightClassKg.Value.ToString("0.##", CultureInfo.InvariantCulture)} kg"
            : "Open";
    }

    private static string BuildFileName(string tournamentName)
    {
        var safeName = new string(tournamentName
            .Select(character => char.IsControl(character) || InvalidFileNameCharacters.Contains(character)
                ? '_'
                : character)
            .ToArray())
            .Trim()
            .Trim('.');

        return $"ergebnisse-{(string.IsNullOrWhiteSpace(safeName) ? "turnier" : safeName)}.csv";
    }

    private static void AppendRow(StringBuilder builder, params string[] values)
    {
        builder.Append(string.Join(';', values.Select(EscapeCsv))).Append("\r\n");
    }

    private static string EscapeCsv(string value)
    {
        return value.IndexOfAny([';', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}