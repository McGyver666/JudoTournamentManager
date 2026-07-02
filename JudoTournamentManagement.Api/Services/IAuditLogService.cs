using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Records and retrieves audit log entries for critical actions.
/// Implementations must never persist sensitive data (credentials, tokens, personal data).
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Appends a single audit log entry and persists it immediately.
    /// </summary>
    Task LogAsync(
        Guid? tournamentId,
        string user,
        string action,
        string entityType,
        Guid? entityId,
        string? details,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns all audit log entries for a tournament, newest first.
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(Guid tournamentId, CancellationToken cancellationToken);
}
