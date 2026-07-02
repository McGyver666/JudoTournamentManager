using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed category storage for offline operation.
/// </summary>
public sealed class SqliteCategoriesStore : ICategoriesStore
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SqliteCategoriesStore> _logger;

    /// <summary>
    /// Initializes a new store instance.
    /// </summary>
    public SqliteCategoriesStore(AppDbContext dbContext, ILogger<SqliteCategoriesStore> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Category>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.AgeGroup)
            .ThenBy(x => x.Name)
            .Select(x => MapToModel(x))
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        return record is null ? null : MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<Category?> CreateAsync(
        Guid tournamentId,
        string name,
        string ageGroup,
        Gender gender,
        decimal? weightClassKg,
        int? minBirthYear,
        int? maxBirthYear,
        string? rulesetNotes,
        int matchDurationSeconds,
        bool goldenScoreEnabled,
        int goldenScoreDurationSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(ageGroup);

        var genderString = gender.ToString();
        var trimmedAgeGroup = ageGroup.Trim();

        var isDuplicate = await _dbContext.Categories.AnyAsync(
            x => x.TournamentId == tournamentId
                 && x.AgeGroup == trimmedAgeGroup
                 && x.Gender == genderString
                 && x.WeightClassKg == weightClassKg,
            cancellationToken);

        if (isDuplicate)
        {
            _logger.LogWarning(
                "Duplicate category (AgeGroup={AgeGroup}, Gender={Gender}, WeightClassKg={WeightClassKg}) for tournament {TournamentId}.",
                trimmedAgeGroup, genderString, weightClassKg, tournamentId);
            return null;
        }

        var utcNow = DateTimeOffset.UtcNow;
        var record = new CategoryRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name.Trim(),
            AgeGroup = trimmedAgeGroup,
            Gender = genderString,
            WeightClassKg = weightClassKg,
            MinBirthYear = minBirthYear,
            MaxBirthYear = maxBirthYear,
            RulesetNotes = rulesetNotes?.Trim(),
            MatchDurationSeconds = matchDurationSeconds > 0 ? matchDurationSeconds : 300,
            GoldenScoreEnabled = goldenScoreEnabled,
            GoldenScoreDurationSeconds = goldenScoreDurationSeconds > 0 ? goldenScoreDurationSeconds : 180,
            IsLocked = false,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        _dbContext.Categories.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Category {CategoryId} created for tournament {TournamentId}.", record.Id, tournamentId);
        return MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        Guid categoryId,
        string name,
        string ageGroup,
        Gender gender,
        decimal? weightClassKg,
        int? minBirthYear,
        int? maxBirthYear,
        string? rulesetNotes,
        int matchDurationSeconds,
        bool goldenScoreEnabled,
        int goldenScoreDurationSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(ageGroup);

        var record = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Category {CategoryId} not found for update.", categoryId);
            return false;
        }

        record.Name = name.Trim();
        record.AgeGroup = ageGroup.Trim();
        record.Gender = gender.ToString();
        record.WeightClassKg = weightClassKg;
        record.MinBirthYear = minBirthYear;
        record.MaxBirthYear = maxBirthYear;
        record.RulesetNotes = rulesetNotes?.Trim();
        record.MatchDurationSeconds = matchDurationSeconds > 0 ? matchDurationSeconds : 300;
        record.GoldenScoreEnabled = goldenScoreEnabled;
        record.GoldenScoreDurationSeconds = goldenScoreDurationSeconds > 0 ? goldenScoreDurationSeconds : 180;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Category {CategoryId} updated.", categoryId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Category {CategoryId} not found for deletion.", categoryId);
            return false;
        }

        _dbContext.Categories.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Category {CategoryId} deleted.", categoryId);
        return true;
    }

    private static Category MapToModel(CategoryRecord record)
    {
        var gender = Enum.Parse<Gender>(record.Gender);
        var drawFormat = record.DrawFormat is not null
            ? Enum.Parse<BracketFormat>(record.DrawFormat)
            : (BracketFormat?)null;
        return new Category(
            record.Id,
            record.TournamentId,
            record.Name,
            record.AgeGroup,
            gender,
            record.WeightClassKg,
            record.MinBirthYear,
            record.MaxBirthYear,
            record.RulesetNotes,
            record.MatchDurationSeconds,
            record.GoldenScoreEnabled,
            record.GoldenScoreDurationSeconds,
            drawFormat,
            record.IsLocked,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
