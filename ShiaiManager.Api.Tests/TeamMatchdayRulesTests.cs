using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;

namespace ShiaiManager.Api.Tests;

/// <summary>
/// Tests the public NWJV team-matchday rule boundary.
/// </summary>
[Trait("Category", "UnitTest")]
public sealed class TeamMatchdayRulesTests
{
    [Fact]
    public void GetProfile_U16Boys_UsesWkoWeightClassesAndActualWeightOnly()
    {
        // Arrange
        var rules = new TeamMatchdayRules();

        // Act
        var profile = rules.GetProfile(TeamMatchdayProfile.U16Boys);

        // Assert
        Assert.Equal([46m, 52m, 58m, 66m, null], profile.WeightClassUpperLimitsKg);
        Assert.False(profile.AllowsHigherWeightClass);
        Assert.Equal(TeamMatchdayFightRules.U15, profile.FightRules);
    }

    [Fact]
    public void CanAssignToWeightClass_U16AthleteInHigherClass_ReturnsFalse()
    {
        // Arrange
        var rules = new TeamMatchdayRules();
        var profile = rules.GetProfile(TeamMatchdayProfile.U16Girls);

        // Act
        var canAssign = rules.CanAssignToWeightClass(profile, 41m, 1);

        // Assert
        Assert.False(canAssign);
    }

    [Fact]
    public void EvaluateEncounter_EqualIndividualWinsWithDifferentUnderScores_ReturnsHikiwake()
    {
        // Arrange
        var rules = new TeamMatchdayRules();

        // Act
        var outcome = rules.EvaluateEncounter(
            homeIndividualWins: 5,
            awayIndividualWins: 5,
            homeUnderScore: 50,
            awayUnderScore: 45);

        // Assert
        Assert.Equal(TeamEncounterOutcome.Hikiwake, outcome);
    }

    [Fact]
    public void GetNoShowResult_SeniorProfile_ReturnsWkoForfeitScore()
    {
        // Arrange
        var rules = new TeamMatchdayRules();

        // Act
        var score = rules.GetNoShowResult(TeamMatchdayProfile.SeniorMen);

        // Assert
        Assert.Equal(0, score.NonAppearingTeamPoints);
        Assert.Equal(2, score.OpposingTeamPoints);
        Assert.Equal(0, score.NonAppearingIndividualWins);
        Assert.Equal(10, score.OpposingIndividualWins);
        Assert.Equal(0, score.NonAppearingUnderScore);
        Assert.Equal(100, score.OpposingUnderScore);
    }
}