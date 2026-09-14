using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ShiaiManager.Api.Services;

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

        var teams = await _dbContext.TeamMatchdayTeams
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var encounters = await _dbContext.TeamEncounters
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var encounterIds = encounters.Select(x => x.Id).ToArray();
        var lineupEntries = await _dbContext.TeamLineupEntries
            .AsNoTracking()
            .Where(x => encounterIds.Contains(x.EncounterId))
            .ToListAsync(cancellationToken);

        var fights = await _dbContext.Fights
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var encounterBouts = await _dbContext.EncounterBouts
            .AsNoTracking()
            .Where(x => encounterIds.Contains(x.EncounterId))
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
            TeamMatchdayTeams = teams,
            TeamEncounters = encounters,
            TeamLineupEntries = lineupEntries,
            Fights = fights,
            EncounterBouts = encounterBouts,
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

        if (backup.TeamMatchdayTeams.Count > 0)
        {
            _dbContext.TeamMatchdayTeams.AddRange(backup.TeamMatchdayTeams);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.TeamEncounters.Count > 0)
        {
            _dbContext.TeamEncounters.AddRange(backup.TeamEncounters);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.TeamLineupEntries.Count > 0)
        {
            _dbContext.TeamLineupEntries.AddRange(backup.TeamLineupEntries);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.Fights.Count > 0)
        {
            _dbContext.Fights.AddRange(backup.Fights);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (backup.EncounterBouts.Count > 0)
        {
            _dbContext.EncounterBouts.AddRange(backup.EncounterBouts);
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
