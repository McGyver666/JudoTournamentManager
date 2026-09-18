using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Data;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Osae-komi (hold-down) rule thresholds in seconds, sourced from tournament settings.
/// </summary>
internal readonly record struct OsaeKomiSettings(int IpponSeconds, int WazaAriSeconds, int YukoSeconds, bool YukoEnabled)
{
    /// <summary>Reads the thresholds from a tournament, applying the DJB default values when unset.</summary>
    public static OsaeKomiSettings FromTournament(TournamentRecord? tournament) => new(
        tournament?.OsaeKomiIpponSeconds ?? 20,
        tournament?.OsaeKomiWazaAriSeconds ?? 10,
        tournament?.OsaeKomiYukoSeconds ?? 5,
        tournament?.OsaeKomiYukoEnabled ?? true);
}

/// <summary>
/// Result of evaluating a completed osae-komi hold: the score to award (if any) and whether it ends the fight.
/// </summary>
internal readonly record struct OsaeKomiHoldOutcome(ScoreType? ScoreToAward, bool ForcesIppon);

/// <summary>
/// Pure osae-komi timing and hold-scoring rules, kept as an internal seam so they can be tested
/// without a full fight setup. This concentrates the DJB hold-down rules in one place.
/// </summary>
internal static class OsaeKomiRules
{
    /// <summary>
    /// The hold duration (seconds) at which the server-authoritative clock must force a stop for the
    /// current holder: waza-ari time when the holder already has a waza-ari (a second one ends the fight),
    /// otherwise ippon time.
    /// </summary>
    public static int EffectiveCapSeconds(bool holderHasWazaAri, OsaeKomiSettings settings) =>
        holderHasWazaAri ? settings.WazaAriSeconds : settings.IpponSeconds;

    /// <summary>
    /// Determines the score awarded for a completed hold of <paramref name="holdSeconds"/> per DJB rules.
    /// A second waza-ari scored by hold-down converts to ippon, and an ippon by hold-down ends the fight.
    /// </summary>
    public static OsaeKomiHoldOutcome EvaluateHold(int holdSeconds, bool holderHasWazaAri, OsaeKomiSettings settings)
    {
        ScoreType? scoreToAward = null;
        if (holdSeconds >= settings.IpponSeconds)
        {
            scoreToAward = ScoreType.Ippon;
        }
        else if (holderHasWazaAri && holdSeconds >= settings.WazaAriSeconds)
        {
            // Second Waza-ari by hold-down converts to Ippon per DJB rules.
            scoreToAward = ScoreType.Ippon;
        }
        else if (holdSeconds >= settings.WazaAriSeconds)
        {
            scoreToAward = ScoreType.WazaAri;
        }
        else if (settings.YukoEnabled && holdSeconds >= settings.YukoSeconds)
        {
            scoreToAward = ScoreType.Yuko;
        }

        return new OsaeKomiHoldOutcome(scoreToAward, scoreToAward == ScoreType.Ippon);
    }
}
