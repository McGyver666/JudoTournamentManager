namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistence model for a single audit log entry recording a critical action.
/// </summary>
public sealed class AuditLogRecord
{
    /// <summary>Unique entry identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tournament the action relates to; null for tournament-independent actions.</summary>
    public Guid? TournamentId { get; set; }

    /// <summary>Timestamp of the action in UTC.</summary>
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>Name of the user who performed the action (never credentials).</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>Action identifier, e.g. "DrawGenerated" or "ResultConfirmed".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Type of the affected entity, e.g. "Category" or "Fight".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Identifier of the affected entity; null when not applicable.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Non-sensitive details (identifiers, counts). Never contains secrets or personal data.</summary>
    public string? Details { get; set; }
}
