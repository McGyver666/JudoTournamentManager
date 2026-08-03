namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Read-only, enriched summary of a single completed fight for the tournament-wide combat overview.
/// Athlete, club, category, and tatami names are resolved server-side so the client needs no extra lookups.
/// </summary>
/// <param name="FightId">Fight identifier (used as a stable client-side row key; not shown to users).</param>
/// <param name="CategoryId">Category the fight belongs to.</param>
/// <param name="CategoryName">Display name of the category.</param>
/// <param name="BracketType">Bracket type ("Main", "Repechage", or "GroupStage").</param>
/// <param name="Round">Round number within the bracket.</param>
/// <param name="FightNumber">1-based position within the round.</param>
/// <param name="PoolNumber">Pool/group number for group-stage fights; null otherwise.</param>
/// <param name="TatamiId">Tatami the fight was fought on; null when it was never assigned.</param>
/// <param name="TatamiName">Display name of the tatami; null when unassigned.</param>
/// <param name="WhiteAthleteName">White athlete's display name ("Last, First").</param>
/// <param name="WhiteClubName">White athlete's club name.</param>
/// <param name="BlueAthleteName">Accent-side athlete's display name ("Last, First").</param>
/// <param name="BlueClubName">Accent-side athlete's club name.</param>
/// <param name="WinnerSide">Winning side ("White" or "Blue"); null when the winner cannot be attributed to a side.</param>
/// <param name="WinnerName">Winner's display name.</param>
/// <param name="WhiteScore">Final accumulated score for the White athlete.</param>
/// <param name="BlueScore">Final accumulated score for the accent-side athlete.</param>
/// <param name="WhitePenalties">Number of penalties (Shido) for the White athlete.</param>
/// <param name="BluePenalties">Number of penalties (Shido) for the accent-side athlete.</param>
/// <param name="WhiteIpponCount">Ippon count for the White athlete.</param>
/// <param name="WhiteWazaAriCount">Waza-ari count for the White athlete.</param>
/// <param name="WhiteYukoCount">Yuko count for the White athlete.</param>
/// <param name="BlueIpponCount">Ippon count for the accent-side athlete.</param>
/// <param name="BlueWazaAriCount">Waza-ari count for the accent-side athlete.</param>
/// <param name="BlueYukoCount">Yuko count for the accent-side athlete.</param>
/// <param name="StartedAtUtc">Timestamp when the fight was started; null when not recorded.</param>
/// <param name="CompletedAtUtc">Timestamp when the fight was completed.</param>
/// <param name="DurationSeconds">Wall-clock duration between start and completion in seconds; null when start or end is missing.</param>
public sealed record CompletedFightSummary(
    Guid FightId,
    Guid CategoryId,
    string CategoryName,
    string BracketType,
    int Round,
    int FightNumber,
    int? PoolNumber,
    Guid? TatamiId,
    string? TatamiName,
    string WhiteAthleteName,
    string WhiteClubName,
    string BlueAthleteName,
    string BlueClubName,
    string? WinnerSide,
    string WinnerName,
    int WhiteScore,
    int BlueScore,
    int WhitePenalties,
    int BluePenalties,
    int WhiteIpponCount,
    int WhiteWazaAriCount,
    int WhiteYukoCount,
    int BlueIpponCount,
    int BlueWazaAriCount,
    int BlueYukoCount,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int? DurationSeconds);
