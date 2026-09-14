using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Provides the NWJV rules that govern a single team matchday.
/// </summary>
public interface ITeamMatchdayRules
{
    /// <summary>
    /// Gets the immutable rule configuration for a supported profile.
    /// </summary>
    TeamMatchdayRuleProfile GetProfile(TeamMatchdayProfile profile);

    /// <summary>
    /// Determines whether an athlete with the confirmed weigh-in may occupy a profile weight class.
    /// </summary>
    bool CanAssignToWeightClass(
        TeamMatchdayRuleProfile profile,
        decimal confirmedWeightKg,
        int weightClassIndex);

    /// <summary>
    /// Gets the WKO forfeit score for a team that does not appear.
    /// </summary>
    TeamNoShowScore GetNoShowResult(TeamMatchdayProfile profile);

    /// <summary>
    /// Determines the completed encounter result from individual-bout wins.
    /// </summary>
    TeamEncounterOutcome EvaluateEncounter(
        int homeIndividualWins,
        int awayIndividualWins,
        int homeUnderScore,
        int awayUnderScore);
}