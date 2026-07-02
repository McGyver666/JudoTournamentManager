namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Aggregated medal count for a single club within a tournament.
/// </summary>
/// <param name="ClubId">Club identifier.</param>
/// <param name="ClubName">Display name of the club.</param>
/// <param name="Gold">Number of gold medals (1st-place finishes).</param>
/// <param name="Silver">Number of silver medals (2nd-place finishes).</param>
/// <param name="Bronze">Number of bronze medals (3rd-place finishes).</param>
public sealed record MedalEntry(Guid ClubId, string ClubName, int Gold, int Silver, int Bronze);
