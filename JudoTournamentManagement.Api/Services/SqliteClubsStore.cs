using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed club storage for offline operation.
/// </summary>
public sealed class SqliteClubsStore : IClubsStore
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SqliteClubsStore> _logger;

    /// <summary>
    /// Initializes a new store instance.
    /// </summary>
    public SqliteClubsStore(AppDbContext dbContext, ILogger<SqliteClubsStore> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Club>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Clubs
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Name)
            .Select(x => MapToModel(x))
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Club?> GetByIdAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Clubs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == clubId, cancellationToken);

        return record is null ? null : MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<Club?> CreateAsync(Guid tournamentId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmedName = name.Trim();
        var isDuplicate = await _dbContext.Clubs.AnyAsync(
            x => x.TournamentId == tournamentId
                 && x.Name.ToLower() == trimmedName.ToLower(),
            cancellationToken);

        if (isDuplicate)
        {
            _logger.LogWarning(
                "Duplicate club name '{ClubName}' for tournament {TournamentId}.", trimmedName, tournamentId);
            return null;
        }

        var utcNow = DateTimeOffset.UtcNow;
        var record = new ClubRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = trimmedName,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        _dbContext.Clubs.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Club {ClubId} created for tournament {TournamentId}.", record.Id, tournamentId);
        return MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Guid clubId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        var record = await _dbContext.Clubs
            .FirstOrDefaultAsync(x => x.Id == clubId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Club {ClubId} not found for update.", clubId);
            return false;
        }

        record.Name = name.Trim();
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Club {ClubId} updated.", clubId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Clubs
            .FirstOrDefaultAsync(x => x.Id == clubId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Club {ClubId} not found for deletion.", clubId);
            return false;
        }

        _dbContext.Clubs.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Club {ClubId} deleted.", clubId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> HasAthletesAsync(Guid clubId, CancellationToken cancellationToken)
    {
        return await _dbContext.Athletes.AnyAsync(x => x.ClubId == clubId, cancellationToken);
    }

    private static Club MapToModel(ClubRecord record) =>
        new(record.Id, record.TournamentId, record.Name, record.CreatedAtUtc, record.UpdatedAtUtc);
}
