namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Turnier-wide (global) club scoring response.
/// </summary>
public sealed record GlobalClubScoringResponse(
    Guid TournamentId,
    DateTimeOffset GeneratedAtUtc,
    string Status,
    int CompletedFights,
    int PlannedFights,
    IReadOnlyList<ClubScoringEntry> Clubs);
