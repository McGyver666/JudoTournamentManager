namespace ShiaiManager.Api.Models;

/// <summary>
/// Whole-tournament statistics for the live control-stand overview.
/// </summary>
public sealed record TournamentOverviewStats(
    Guid TournamentId,
    int AthleteCount,
    int ClubCount,
    int CategoryCount,
    int FightsCompleted,
    int FightsTotal,
    double? AverageFightDurationSeconds,
    int IpponCount);