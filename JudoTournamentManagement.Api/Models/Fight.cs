namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Represents a single fight within a bracket.
/// </summary>
/// <param name="Id">Unique fight identifier.</param>
/// <param name="TournamentId">Owning tournament identifier.</param>
/// <param name="CategoryId">Category this fight belongs to.</param>
/// <param name="BracketType">Whether this is a main-bracket or repechage fight.</param>
/// <param name="Round">Round number (1 = first round, highest = final).</param>
/// <param name="FightNumber">1-based position within the round.</param>
/// <param name="PoolNumber">Pool/group number for round-robin group-stage fights; null for all other fight types.</param>
/// <param name="WhiteSourceFightId">Source fight for the White slot; null for directly assigned slots.</param>
/// <param name="WhiteSourceOutcome">Outcome selected from the White slot source fight.</param>
/// <param name="BlueSourceFightId">Source fight for the Blue slot; null for directly assigned slots.</param>
/// <param name="BlueSourceOutcome">Outcome selected from the Blue slot source fight.</param>
/// <param name="WhiteAthleteId">Athlete in the White role; null when TBD.</param>
/// <param name="BlueAthleteId">Athlete in the Blue role; null for a bye or TBD.</param>
/// <param name="WinnerId">Winner identifier; null until fight is completed.</param>
/// <param name="IsBye">True when one slot is a bye and the other athlete auto-advances.</param>
/// <param name="Status">Current lifecycle state of the fight.</param>
/// <param name="TatamiId">Tatami this fight is assigned to; null when unassigned.</param>
/// <param name="WhiteScore">Accumulated score for the White athlete.</param>
/// <param name="BlueScore">Accumulated score for the Blue athlete.</param>
/// <param name="WhitePenalties">Number of penalties (Shido) for the White athlete.</param>
/// <param name="BluePenalties">Number of penalties (Shido) for the Blue athlete.</param>
/// <param name="WhiteIpponCount">Number of Ippon scores recorded for the White athlete.</param>
/// <param name="WhiteWazaAriCount">Number of Waza-ari scores recorded for the White athlete.</param>
/// <param name="WhiteYukoCount">Number of Yuko scores recorded for the White athlete.</param>
/// <param name="BlueIpponCount">Number of Ippon scores recorded for the Blue athlete.</param>
/// <param name="BlueWazaAriCount">Number of Waza-ari scores recorded for the Blue athlete.</param>
/// <param name="BlueYukoCount">Number of Yuko scores recorded for the Blue athlete.</param>
/// <param name="PausedAtUtc">Timestamp when the fight was paused; null while running.</param>
/// <param name="OsaeKomiSide">Side that currently has an active osae-komi hold; null when inactive.</param>
/// <param name="OsaeKomiStartedAtUtc">Timestamp when the active osae-komi hold started.</param>
/// <param name="StartedAtUtc">Timestamp when the fight was started; null while pending.</param>
/// <param name="CompletedAtUtc">Timestamp when the fight was completed; null until confirmed.</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
/// <param name="UpdatedAtUtc">Last update timestamp in UTC.</param>
/// <param name="IsGoldenScore">
/// True when the fight is currently in the golden-score overtime phase:
/// the fight is active (InProgress or Paused), golden score is enabled for the category,
/// and the elapsed fight time has exceeded the regular match duration.
/// </param>
public sealed record Fight(
    Guid Id,
    Guid TournamentId,
    Guid CategoryId,
    FightBracketType BracketType,
    int Round,
    int FightNumber,
    int? PoolNumber,
    Guid? WhiteSourceFightId,
    FightSlotSourceOutcome? WhiteSourceOutcome,
    Guid? BlueSourceFightId,
    FightSlotSourceOutcome? BlueSourceOutcome,
    Guid? WhiteAthleteId,
    Guid? BlueAthleteId,
    Guid? WinnerId,
    bool IsBye,
    FightStatus Status,
    Guid? TatamiId,
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
    DateTimeOffset? PausedAtUtc,
    string? OsaeKomiSide,
    DateTimeOffset? OsaeKomiStartedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsGoldenScore);
