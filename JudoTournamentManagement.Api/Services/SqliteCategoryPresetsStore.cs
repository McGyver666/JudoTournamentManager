using System.Text.Json;
using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed tournament category preset storage.
/// </summary>
public sealed class SqliteCategoryPresetsStore : ICategoryPresetsStore
{
    // Default DJB/NWJV standard preset rows encoded as age offsets so they are
    // year-independent. MaxAgeYears = age of oldest allowed athlete (→ minBirthYear),
    // MinAgeYears = age of youngest allowed athlete (→ maxBirthYear), null = open.
    private static readonly IReadOnlyList<DefaultPresetRow> DefaultRows =
    [
        // Female
        new("U11",    Gender.Female, 10, 8,    120, [22m, 24m, 26m, 28m, 30m, 33m, 36m, 40m, 44m, 48m, null]),
        new("U13",    Gender.Female, 12, 10,   180, [27m, 30m, 33m, 36m, 40m, 44m, 48m, 52m, 57m, null]),
        new("U15",    Gender.Female, 14, 12,   180, [33m, 36m, 40m, 44m, 48m, 52m, 57m, 63m, null]),
        new("U18",    Gender.Female, 17, 15,   240, [40m, 44m, 48m, 52m, 57m, 63m, 70m, 78m, null]),
        new("U21",    Gender.Female, 20, 17,   240, [48m, 52m, 57m, 63m, 70m, 78m, null]),
        new("Frauen", Gender.Female, 17, null, 240, [48m, 52m, 57m, 63m, 70m, 78m, null]),

        // Male
        new("U11",    Gender.Male, 10, 8,    120, [23m, 25m, 27m, 29m, 31m, 34m, 37m, 40m, 43m, 46m, null]),
        new("U13",    Gender.Male, 12, 10,   180, [28m, 31m, 34m, 37m, 40m, 43m, 46m, 50m, 55m, null]),
        new("U15",    Gender.Male, 14, 12,   180, [34m, 37m, 40m, 43m, 46m, 50m, 55m, 60m, 66m, null]),
        new("U18",    Gender.Male, 17, 15,   240, [46m, 50m, 55m, 60m, 66m, 73m, 81m, 90m, null]),
        new("U21",    Gender.Male, 20, 17,   240, [60m, 66m, 73m, 81m, 90m, 100m, null]),
        new("Männer", Gender.Male, 17, null, 240, [60m, 66m, 73m, 81m, 90m, 100m, null]),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;

    public SqliteCategoryPresetsStore(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TournamentCategoryPreset>> GetAllAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _dbContext.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);

        var tournamentYear = tournament?.Date.Year ?? DateTimeOffset.UtcNow.Year;

        var records = await _dbContext.CategoryPresets
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.AgeGroup)
            .ToArrayAsync(cancellationToken);

        return records.Select(r => MapToModel(r, tournamentYear)).ToArray();
    }

    /// <inheritdoc />
    public async Task SeedDefaultsAsync(Guid tournamentId, int tournamentYear, CancellationToken cancellationToken)
    {
        var alreadyExists = await _dbContext.CategoryPresets
            .AnyAsync(x => x.TournamentId == tournamentId, cancellationToken);

        if (alreadyExists)
        {
            return;
        }

        var records = DefaultRows.Select((row, index) => new CategoryPresetRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            AgeGroup = row.AgeGroup,
            Gender = row.Gender.ToString(),
            MaxAgeYears = row.MaxAgeYears,
            MinAgeYears = row.MinAgeYears,
            DefaultMatchDurationSeconds = row.DefaultMatchDurationSeconds,
            WeightClassLimitsJson = JsonSerializer.Serialize(row.WeightClassLimitsKg, JsonOptions),
            SortOrder = index,
        }).ToArray();

        _dbContext.CategoryPresets.AddRange(records);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReplaceAllAsync(
        Guid tournamentId,
        IReadOnlyList<TournamentCategoryPreset> presets,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.CategoryPresets
            .Where(x => x.TournamentId == tournamentId)
            .ToArrayAsync(cancellationToken);

        _dbContext.CategoryPresets.RemoveRange(existing);

        var records = presets.Select((p, index) => new CategoryPresetRecord
        {
            Id = p.Id == Guid.Empty ? Guid.NewGuid() : p.Id,
            TournamentId = tournamentId,
            AgeGroup = p.AgeGroup.Trim(),
            Gender = p.Gender.ToString(),
            MaxAgeYears = p.MaxAgeYears,
            MinAgeYears = p.MinAgeYears,
            DefaultMatchDurationSeconds = p.DefaultMatchDurationSeconds,
            WeightClassLimitsJson = JsonSerializer.Serialize(p.WeightClassLimitsKg, JsonOptions),
            SortOrder = index,
        }).ToArray();

        _dbContext.CategoryPresets.AddRange(records);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TournamentCategoryPreset MapToModel(CategoryPresetRecord record, int tournamentYear)
    {
        var gender = Enum.Parse<Gender>(record.Gender);
        var weights = DeserializeWeights(record.WeightClassLimitsJson);
        var minBirthYear = record.MaxAgeYears.HasValue ? tournamentYear - record.MaxAgeYears.Value : (int?)null;
        var maxBirthYear = record.MinAgeYears.HasValue ? tournamentYear - record.MinAgeYears.Value : (int?)null;

        return new TournamentCategoryPreset(
            record.Id,
            record.TournamentId,
            record.AgeGroup,
            gender,
            record.MaxAgeYears,
            record.MinAgeYears,
            minBirthYear,
            maxBirthYear,
            record.DefaultMatchDurationSeconds,
            weights,
            record.SortOrder);
    }

    private static IReadOnlyList<decimal?> DeserializeWeights(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<decimal?[]>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed record DefaultPresetRow(
        string AgeGroup,
        Gender Gender,
        int? MaxAgeYears,
        int? MinAgeYears,
        int DefaultMatchDurationSeconds,
        IReadOnlyList<decimal?> WeightClassLimitsKg);
}
