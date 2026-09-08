using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Implements NWJV team-matchday rules from the 25 April 2026 WKO.
/// </summary>
public sealed class TeamMatchdayRules : ITeamMatchdayRules
{
    private static readonly IReadOnlyDictionary<TeamMatchdayProfile, TeamMatchdayRuleProfile> Profiles =
        new Dictionary<TeamMatchdayProfile, TeamMatchdayRuleProfile>
        {
            [TeamMatchdayProfile.SeniorMen] = new(
                TeamMatchdayProfile.SeniorMen,
                [66m, 73m, 81m, 90m, null],
                true,
                TeamMatchdayFightRules.Senior,
                240),
            [TeamMatchdayProfile.SeniorWomen] = new(
                TeamMatchdayProfile.SeniorWomen,
                [52m, 57m, 63m, 70m, null],
                true,
                TeamMatchdayFightRules.Senior,
                240),
            [TeamMatchdayProfile.U16Boys] = new(
                TeamMatchdayProfile.U16Boys,
                [46m, 52m, 58m, 66m, null],
                false,
                TeamMatchdayFightRules.U15,
                240),
            [TeamMatchdayProfile.U16Girls] = new(
                TeamMatchdayProfile.U16Girls,
                [42m, 47m, 53m, 60m, null],
                false,
                TeamMatchdayFightRules.U15,
                240)
        };

    /// <inheritdoc />
    public TeamMatchdayRuleProfile GetProfile(TeamMatchdayProfile profile)
    {
        return Profiles.TryGetValue(profile, out var ruleProfile)
            ? ruleProfile
            : throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unbekanntes Landesliga-Regelprofil.");
    }

    /// <inheritdoc />
    public bool CanAssignToWeightClass(
        TeamMatchdayRuleProfile profile,
        decimal confirmedWeightKg,
        int weightClassIndex)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (confirmedWeightKg <= 0
            || weightClassIndex < 0
            || weightClassIndex >= profile.WeightClassUpperLimitsKg.Count)
        {
            return false;
        }

        var actualWeightClassIndex = ResolveWeightClassIndex(profile.WeightClassUpperLimitsKg, confirmedWeightKg);
        return actualWeightClassIndex >= 0
            && (profile.AllowsHigherWeightClass
                ? weightClassIndex >= actualWeightClassIndex
                : weightClassIndex == actualWeightClassIndex);
    }

    /// <inheritdoc />
    public TeamNoShowScore GetNoShowResult(TeamMatchdayProfile profile)
    {
        _ = GetProfile(profile);
        return new TeamNoShowScore(0, 2, 0, 10, 0, 100);
    }

    /// <inheritdoc />
    public TeamEncounterOutcome EvaluateEncounter(
        int homeIndividualWins,
        int awayIndividualWins,
        int homeUnderScore,
        int awayUnderScore)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(homeIndividualWins);
        ArgumentOutOfRangeException.ThrowIfNegative(awayIndividualWins);
        ArgumentOutOfRangeException.ThrowIfNegative(homeUnderScore);
        ArgumentOutOfRangeException.ThrowIfNegative(awayUnderScore);

        return homeIndividualWins.CompareTo(awayIndividualWins) switch
        {
            > 0 => TeamEncounterOutcome.HomeWin,
            < 0 => TeamEncounterOutcome.AwayWin,
            _ => TeamEncounterOutcome.Hikiwake
        };
    }

    private static int ResolveWeightClassIndex(IReadOnlyList<decimal?> upperLimitsKg, decimal weightKg)
    {
        for (var index = 0; index < upperLimitsKg.Count; index++)
        {
            if (upperLimitsKg[index] is null || weightKg <= upperLimitsKg[index])
            {
                return index;
            }
        }

        return -1;
    }
}