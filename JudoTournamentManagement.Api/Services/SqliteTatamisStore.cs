using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed tatami storage for offline operation.
/// </summary>
public sealed class SqliteTatamisStore : ITatamisStore
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SqliteTatamisStore> _logger;

    /// <summary>
    /// Initializes a new store instance.
    /// </summary>
    public SqliteTatamisStore(AppDbContext dbContext, ILogger<SqliteTatamisStore> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tatami>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Tatamis
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => MapToModel(x))
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Tatami?> GetByIdAsync(Guid tatamisId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Tatamis
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tatamisId, cancellationToken);

        return record is null ? null : MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<Tatami> CreateAsync(
        Guid tournamentId,
        string name,
        int? displayOrder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        var order = displayOrder
            ?? (await _dbContext.Tatamis
                    .Where(x => x.TournamentId == tournamentId)
                    .MaxAsync(x => (int?)x.DisplayOrder, cancellationToken) ?? -1) + 1;

        var utcNow = DateTimeOffset.UtcNow;
        var record = new TatamiRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name.Trim(),
            DisplayOrder = order,
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        _dbContext.Tatamis.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tatami {TatamisId} created for tournament {TournamentId}.", record.Id, tournamentId);
        return MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        Guid tatamisId,
        string name,
        int displayOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        var record = await _dbContext.Tatamis
            .FirstOrDefaultAsync(x => x.Id == tatamisId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Tatami {TatamisId} not found for update.", tatamisId);
            return false;
        }

        record.Name = name.Trim();
        record.DisplayOrder = displayOrder;
        record.IsActive = isActive;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Tatami {TatamisId} updated.", tatamisId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid tatamisId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Tatamis
            .FirstOrDefaultAsync(x => x.Id == tatamisId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Tatami {TatamisId} not found for deletion.", tatamisId);
            return false;
        }

        _dbContext.Tatamis.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Tatami {TatamisId} deleted.", tatamisId);
        return true;
    }

    private static Tatami MapToModel(TatamiRecord record) =>
        new(record.Id,
            record.TournamentId,
            record.Name,
            record.DisplayOrder,
            record.IsActive,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
}
