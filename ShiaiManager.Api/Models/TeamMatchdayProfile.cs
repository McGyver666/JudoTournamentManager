using System.Text.Json.Serialization;

namespace ShiaiManager.Api.Models;

/// <summary>
/// Identifies a supported NWJV team-matchday rule profile.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamMatchdayProfile
{
    /// <summary>Senior men's Landesliga profile.</summary>
    SeniorMen,

    /// <summary>Senior women's Landesliga profile.</summary>
    SeniorWomen,

    /// <summary>U16 boys' league profile.</summary>
    U16Boys,

    /// <summary>U16 girls' league profile.</summary>
    U16Girls
}

/// <summary>
/// Identifies the individual-fight rules used by a team-matchday profile.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamMatchdayFightRules
{
    /// <summary>Senior fight rules with four-minute regular time.</summary>
    Senior,

    /// <summary>U15 fight rules, as required for U16 league matches.</summary>
    U15
}

/// <summary>
/// Result of a completed team encounter.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamEncounterOutcome
{
    /// <summary>The home team won more individual bouts.</summary>
    HomeWin,

    /// <summary>The away team won more individual bouts.</summary>
    AwayWin,

    /// <summary>Both teams won the same number of individual bouts.</summary>
    Hikiwake
}

/// <summary>
/// Immutable NWJV rule configuration for one team-matchday profile.
/// </summary>
/// <param name="Profile">Profile identifier.</param>
/// <param name="WeightClassUpperLimitsKg">Ascending upper limits; null identifies the open class.</param>
/// <param name="AllowsHigherWeightClass">Whether athletes may be assigned above their actual-weight class.</param>
/// <param name="FightRules">Individual-fight rule set.</param>
/// <param name="MatchDurationSeconds">Regular fight duration.</param>
public sealed record TeamMatchdayRuleProfile(
    TeamMatchdayProfile Profile,
    IReadOnlyList<decimal?> WeightClassUpperLimitsKg,
    bool AllowsHigherWeightClass,
    TeamMatchdayFightRules FightRules,
    int MatchDurationSeconds);

/// <summary>
/// WKO score awarded when a team does not appear for an encounter.
/// </summary>
/// <param name="NonAppearingTeamPoints">Team points recorded for the absent team.</param>
/// <param name="OpposingTeamPoints">Team points recorded for the present team.</param>
/// <param name="NonAppearingIndividualWins">Individual wins recorded for the absent team.</param>
/// <param name="OpposingIndividualWins">Individual wins recorded for the present team.</param>
/// <param name="NonAppearingUnderScore">Under-score recorded for the absent team.</param>
/// <param name="OpposingUnderScore">Under-score recorded for the present team.</param>
public sealed record TeamNoShowScore(
    int NonAppearingTeamPoints,
    int OpposingTeamPoints,
    int NonAppearingIndividualWins,
    int OpposingIndividualWins,
    int NonAppearingUnderScore,
    int OpposingUnderScore);