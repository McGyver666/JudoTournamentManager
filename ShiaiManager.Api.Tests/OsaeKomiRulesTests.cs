using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Data;
using ShiaiManager.Api.Services;

namespace ShiaiManager.Api.Tests;

/// <summary>
/// Characterizes the osae-komi (hold-down) timing and scoring rules as a standalone seam,
/// so the DJB thresholds can be verified without a full fight setup (issue #48).
/// </summary>
public sealed class OsaeKomiRulesTests
{
    private static readonly OsaeKomiSettings DefaultSettings = new(
        IpponSeconds: 20, WazaAriSeconds: 10, YukoSeconds: 5, YukoEnabled: true);

    [Fact]
    [Trait("Category", "UnitTest")]
    public void FromTournament_NullTournament_UsesDjbDefaults()
    {
        var settings = OsaeKomiSettings.FromTournament(null);

        Assert.Equal(20, settings.IpponSeconds);
        Assert.Equal(10, settings.WazaAriSeconds);
        Assert.Equal(5, settings.YukoSeconds);
        Assert.True(settings.YukoEnabled);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void FromTournament_ReadsConfiguredThresholds()
    {
        var tournament = new TournamentRecord
        {
            Id = Guid.NewGuid(),
            Name = "T",
            Date = new DateOnly(2026, 1, 1),
            Venue = "V",
            Organizer = "O",
            OsaeKomiIpponSeconds = 15,
            OsaeKomiWazaAriSeconds = 7,
            OsaeKomiYukoSeconds = 3,
            OsaeKomiYukoEnabled = false,
        };

        var settings = OsaeKomiSettings.FromTournament(tournament);

        Assert.Equal(15, settings.IpponSeconds);
        Assert.Equal(7, settings.WazaAriSeconds);
        Assert.Equal(3, settings.YukoSeconds);
        Assert.False(settings.YukoEnabled);
    }

    [Theory]
    [Trait("Category", "UnitTest")]
    [InlineData(false, 20)] // holder without waza-ari is capped at ippon time
    [InlineData(true, 10)]  // holder with a waza-ari is capped at waza-ari time (second waza-ari ends it)
    public void EffectiveCapSeconds_DependsOnHolderWazaAri(bool holderHasWazaAri, int expectedCap)
    {
        var cap = OsaeKomiRules.EffectiveCapSeconds(holderHasWazaAri, DefaultSettings);

        Assert.Equal(expectedCap, cap);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void EvaluateHold_BelowYukoThreshold_AwardsNothing()
    {
        var outcome = OsaeKomiRules.EvaluateHold(holdSeconds: 4, holderHasWazaAri: false, DefaultSettings);

        Assert.Null(outcome.ScoreToAward);
        Assert.False(outcome.ForcesIppon);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void EvaluateHold_AtYukoThreshold_AwardsYuko()
    {
        var outcome = OsaeKomiRules.EvaluateHold(holdSeconds: 5, holderHasWazaAri: false, DefaultSettings);

        Assert.Equal(ScoreType.Yuko, outcome.ScoreToAward);
        Assert.False(outcome.ForcesIppon);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void EvaluateHold_YukoDisabled_BelowWazaAri_AwardsNothing()
    {
        var settings = DefaultSettings with { YukoEnabled = false };

        var outcome = OsaeKomiRules.EvaluateHold(holdSeconds: 6, holderHasWazaAri: false, settings);

        Assert.Null(outcome.ScoreToAward);
        Assert.False(outcome.ForcesIppon);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void EvaluateHold_AtWazaAriThreshold_AwardsWazaAri()
    {
        var outcome = OsaeKomiRules.EvaluateHold(holdSeconds: 10, holderHasWazaAri: false, DefaultSettings);

        Assert.Equal(ScoreType.WazaAri, outcome.ScoreToAward);
        Assert.False(outcome.ForcesIppon);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void EvaluateHold_SecondWazaAriByHold_ConvertsToIpponAndEndsFight()
    {
        var outcome = OsaeKomiRules.EvaluateHold(holdSeconds: 10, holderHasWazaAri: true, DefaultSettings);

        Assert.Equal(ScoreType.Ippon, outcome.ScoreToAward);
        Assert.True(outcome.ForcesIppon);
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void EvaluateHold_AtIpponThreshold_AwardsIpponAndEndsFight()
    {
        var outcome = OsaeKomiRules.EvaluateHold(holdSeconds: 20, holderHasWazaAri: false, DefaultSettings);

        Assert.Equal(ScoreType.Ippon, outcome.ScoreToAward);
        Assert.True(outcome.ForcesIppon);
    }
}
