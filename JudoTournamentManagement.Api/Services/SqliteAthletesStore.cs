using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed athlete storage for offline operation.
/// </summary>
public sealed class SqliteAthletesStore : IAthletesStore
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SqliteAthletesStore> _logger;

    /// <summary>
    /// Initializes a new store instance.
    /// </summary>
    public SqliteAthletesStore(AppDbContext dbContext, ILogger<SqliteAthletesStore> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Athlete>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Athletes
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => MapToModel(x))
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Athlete?> GetByIdAsync(Guid athleteId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Athletes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == athleteId, cancellationToken);

        return record is null ? null : MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<Athlete?> CreateAsync(
        Guid tournamentId,
        Guid clubId,
        string firstName,
        string lastName,
        int birthYear,
        Gender gender,
        string? licenseId,
        decimal? weightKg,
        int grade,
        bool allowDuplicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(firstName);
        ArgumentNullException.ThrowIfNull(lastName);

        var trimmedFirst = firstName.Trim();
        var trimmedLast = lastName.Trim();

        if (!allowDuplicate)
        {
            var isDuplicate = await _dbContext.Athletes.AnyAsync(
                x => x.TournamentId == tournamentId
                     && x.ClubId == clubId
                     && x.FirstName == trimmedFirst
                     && x.LastName == trimmedLast
                     && x.BirthYear == birthYear,
                cancellationToken);

            if (isDuplicate)
            {
                _logger.LogWarning(
                    "Possible duplicate athlete '{LastName}, {FirstName}' ({BirthYear}) for club {ClubId} in tournament {TournamentId}.",
                    trimmedLast, trimmedFirst, birthYear, clubId, tournamentId);
                return null;
            }
        }

        var utcNow = DateTimeOffset.UtcNow;
        var record = new AthleteRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            ClubId = clubId,
            FirstName = trimmedFirst,
            LastName = trimmedLast,
            BirthYear = birthYear,
            Gender = gender.ToString(),
            LicenseId = licenseId?.Trim(),
            WeightKg = weightKg,
            Grade = grade,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        _dbContext.Athletes.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Athlete {AthleteId} created for tournament {TournamentId}.", record.Id, tournamentId);
        return MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        Guid athleteId,
        Guid clubId,
        string firstName,
        string lastName,
        int birthYear,
        Gender gender,
        string? licenseId,
        decimal? weightKg,
        int grade,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(firstName);
        ArgumentNullException.ThrowIfNull(lastName);

        var record = await _dbContext.Athletes
            .FirstOrDefaultAsync(x => x.Id == athleteId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Athlete {AthleteId} not found for update.", athleteId);
            return false;
        }

        record.ClubId = clubId;
        record.FirstName = firstName.Trim();
        record.LastName = lastName.Trim();
        record.BirthYear = birthYear;
        record.Gender = gender.ToString();
        record.LicenseId = licenseId?.Trim();
        record.WeightKg = weightKg;
        record.Grade = grade;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Athlete {AthleteId} updated.", athleteId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid athleteId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Athletes
            .FirstOrDefaultAsync(x => x.Id == athleteId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Athlete {AthleteId} not found for deletion.", athleteId);
            return false;
        }

        _dbContext.Athletes.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Athlete {AthleteId} deleted.", athleteId);
        return true;
    }

    private static Athlete MapToModel(AthleteRecord record) =>
        new(record.Id,
            record.TournamentId,
            record.ClubId,
            record.FirstName,
            record.LastName,
            record.BirthYear,
            Enum.Parse<Gender>(record.Gender),
            record.LicenseId,
            record.WeightKg,
            record.Grade,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
}
