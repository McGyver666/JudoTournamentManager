namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Represents a club (Verein) participating in a tournament.
/// </summary>
/// <param name="Id">Unique club identifier.</param>
/// <param name="TournamentId">Owning tournament identifier.</param>
/// <param name="Name">Club display name; unique within the tournament.</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
/// <param name="UpdatedAtUtc">Last update timestamp in UTC.</param>
public sealed record Club(
    Guid Id,
    Guid TournamentId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
