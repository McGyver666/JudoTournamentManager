using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Persists team-matchday configuration within a tournament in <see cref="CompetitionMode.TeamMatchday"/> mode.
/// </summary>
public interface ITeamMatchdayStore
{
    /// <summary>
    /// Gets a team matchday, or <c>null</c> when the tournament is not in team-matchday mode.
    /// </summary>
    Task<TeamMatchday?> GetAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a team representing an existing tournament club.
    /// Returns <c>null</c> when the tournament is not a team matchday or the club is invalid.
    /// </summary>
    Task<TeamMatchdayTeam?> AddTeamAsync(
        Guid tournamentId,
        Guid clubId,
        string name,
        CancellationToken cancellationToken);

    /// <summary>
    /// Confirms an athlete's actual weight for the matchday.
    /// Returns <c>null</c> when the tournament is not a team matchday or the athlete is invalid.
    /// </summary>
    Task<MatchdayWeighIn?> ConfirmWeighInAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        CancellationToken cancellationToken);

    /// <summary>
    /// Draws and stores the shared weight-class order before encounter bouts start.
    /// </summary>
    Task<IReadOnlyList<int>?> DrawWeightClassOrderAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a pairing between two configured teams.
    /// </summary>
    Task<TeamEncounter?> CreateEncounterAsync(
        Guid tournamentId,
        Guid homeTeamId,
        Guid awayTeamId,
        Guid? tatamiId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the saved lineup entries for one encounter leg.
    /// </summary>
    Task<IReadOnlyList<TeamLineupEntry>> GetLineupAsync(Guid encounterId, int legNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces one team's lineup before the encounter leg is prepared.
    /// </summary>
    Task<TeamMatchdayOperationResult> ReplaceLineupAsync(
        Guid tournamentId,
        Guid encounterId,
        int legNumber,
        Guid teamId,
        IReadOnlyList<TeamLineupAssignment> assignments,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates standard pending fights from both lineups in the drawn weight-class order.
    /// </summary>
    Task<IReadOnlyList<EncounterBout>?> PrepareEncounterLegAsync(
        Guid tournamentId,
        Guid encounterId,
        int legNumber,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a no-show for one team before normal encounter bouts are prepared.
    /// </summary>
    Task<TeamMatchdayOperationResult> RecordNoShowAsync(
        Guid tournamentId,
        Guid encounterId,
        Guid noShowTeamId,
        CancellationToken cancellationToken);
}