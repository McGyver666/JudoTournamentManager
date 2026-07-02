namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Represents a single audit log entry for a critical action.
/// </summary>
/// <param name="Id">Unique entry identifier.</param>
/// <param name="TournamentId">Tournament the action relates to; null when independent.</param>
/// <param name="TimestampUtc">Timestamp of the action in UTC.</param>
/// <param name="User">Name of the user who performed the action (never credentials).</param>
/// <param name="Action">Action identifier, e.g. "DrawGenerated".</param>
/// <param name="EntityType">Type of the affected entity, e.g. "Fight".</param>
/// <param name="EntityId">Identifier of the affected entity; null when not applicable.</param>
/// <param name="Details">Non-sensitive details (identifiers, counts).</param>
public sealed record AuditLogEntry(
    Guid Id,
    Guid? TournamentId,
    DateTimeOffset TimestampUtc,
    string User,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? Details);
