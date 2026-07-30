namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Club scoring response grouped by age groups for one tournament.
/// </summary>
public sealed record AgeGroupClubScoringResponse(
    Guid TournamentId,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<AgeGroupClubScoringItem> Items);

/// <summary>
/// One age-group block in the club scoring response.
/// </summary>
public sealed record AgeGroupClubScoringItem(
    string AgeGroup,
    string Status,
    int CompletedFights,
    int PlannedFights,
    IReadOnlyList<ClubScoringEntry> Clubs);
