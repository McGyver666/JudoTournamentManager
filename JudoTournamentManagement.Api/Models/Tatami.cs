namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Represents a competition area (tatami) within a tournament.
/// </summary>
/// <param name="Id">Unique tatami identifier.</param>
/// <param name="TournamentId">Owning tournament identifier.</param>
/// <param name="Name">Display name, e.g. "Tatami 1".</param>
/// <param name="DisplayOrder">Position in the display/queue sequence (0-based).</param>
/// <param name="IsActive">Whether this tatami is currently in use.</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
/// <param name="UpdatedAtUtc">Last update timestamp in UTC.</param>
public sealed record Tatami(
    Guid Id,
    Guid TournamentId,
    string Name,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
