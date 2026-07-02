using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed audit log for critical tournament actions (I-01).
/// Only non-sensitive identifiers and counts are stored; no credentials or personal data.
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AuditLogService> _logger;

    /// <summary>Initializes a new service instance.</summary>
    public AuditLogService(AppDbContext dbContext, ILogger<AuditLogService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogAsync(
        Guid? tournamentId,
        string user,
        string action,
        string entityType,
        Guid? entityId,
        string? details,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        var record = new AuditLogRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            TimestampUtc = DateTimeOffset.UtcNow,
            User = string.IsNullOrWhiteSpace(user) ? "unbekannt" : user.Trim(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        };

        _dbContext.AuditLogs.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Audit: {Action} on {EntityType} {EntityId} by {User}.",
            action, entityType, entityId, record.User);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x => new AuditLogEntry(
                x.Id,
                x.TournamentId,
                x.TimestampUtc,
                x.User,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.Details))
            .ToArrayAsync(cancellationToken);
    }
}
