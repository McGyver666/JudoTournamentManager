using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Implements tournament backup and restore using EF Core directly.
/// </summary>
public sealed class BackupService : IBackupService
{
    private const string SupportedVersion = "1.0";

    private readonly AppDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="BackupService"/>.
    /// </summary>
    public BackupService(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<TournamentBackup?> BackupAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _dbContext.Tournaments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);

        if (tournament is null)
        {
            return null;
        }

        var tatamis = await _dbContext.Tatamis
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var clubs = await _dbContext.Clubs
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var athletes = await _dbContext.Athletes
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var registrations = await _dbContext.Registrations
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var fights = await _dbContext.Fights
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var auditLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        return new TournamentBackup
        {
            Version = SupportedVersion,
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Tournament = tournament,
            Tatamis = tatamis,
            Categories = categories,
            Clubs = clubs,
            Athletes = athletes,
            Registrations = registrations,
            Fights = fights,
            AuditLogs = auditLogs
        };
    }

    /// <inheritdoc />
    public async Task<RestoreResult> RestoreAsync(TournamentBackup backup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);

        if (!string.Equals(backup.Version, SupportedVersion, StringComparison.Ordinal))
        {
            return new RestoreResult(false, "UnsupportedVersion",
                $"Backup-Version '{backup.Version}' wird nicht unterstützt. Erwartet: '{SupportedVersion}'.");
        }

        if (backup.Tournament is null)
        {
            return new RestoreResult(false, "InvalidBackup", "Backup enthält kein Turnier.");
        }

        var exists = await _dbContext.Tournaments
            .AnyAsync(x => x.Id == backup.Tournament.Id, cancellationToken);

        if (exists)
        {
            return new RestoreResult(false, "TournamentAlreadyExists",
                $"Ein Turnier mit der ID '{backup.Tournament.Id}' existiert bereits.");
        }

        // Detach navigation properties to avoid EF tracking conflicts during bulk insert.
        // (Navigation properties are already null since records were loaded with AsNoTracking.)

        // Insert in FK dependency order.
        _dbContext.Tournaments.Add(backup.Tournament);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (backup.Tatamis.Count > 0)
        {
            _dbContext.Tatamis.AddRange(backup.Tatamis);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.Categories.Count > 0)
        {
            _dbContext.Categories.AddRange(backup.Categories);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.Clubs.Count > 0)
        {
            _dbContext.Clubs.AddRange(backup.Clubs);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.Athletes.Count > 0)
        {
            _dbContext.Athletes.AddRange(backup.Athletes);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.Registrations.Count > 0)
        {
            _dbContext.Registrations.AddRange(backup.Registrations);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.Fights.Count > 0)
        {
            _dbContext.Fights.AddRange(backup.Fights);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.AuditLogs.Count > 0)
        {
            _dbContext.AuditLogs.AddRange(backup.AuditLogs);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new RestoreResult(true, null, null);
    }
}
