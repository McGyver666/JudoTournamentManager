namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Result of a restore operation.
/// </summary>
public sealed record RestoreResult(bool Success, string? ErrorCode, string? ErrorMessage);

/// <summary>
/// Service for exporting and restoring full tournament backups.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a complete backup snapshot of the specified tournament.
    /// Returns null if the tournament does not exist.
    /// </summary>
    Task<Contracts.TournamentBackup?> BackupAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Restores a tournament from a backup snapshot.
    /// Fails if a tournament with the same ID already exists.
    /// </summary>
    Task<RestoreResult> RestoreAsync(Contracts.TournamentBackup backup, CancellationToken cancellationToken);
}
