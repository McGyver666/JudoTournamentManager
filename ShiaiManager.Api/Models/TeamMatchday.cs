namespace ShiaiManager.Api.Models;

/// <summary>
/// Read model for the configurable state of a team matchday.
/// </summary>
/// <param name="TournamentId">Owning team-matchday tournament.</param>
/// <param name="Profile">Selected NWJV rule profile.</param>
/// <param name="Teams">Teams entered for the matchday.</param>
/// <param name="WeighIns">Confirmed matchday weigh-ins.</param>
public sealed record TeamMatchday(
    Guid TournamentId,
    TeamMatchdayProfile Profile,
    IReadOnlyList<TeamMatchdayTeam> Teams,
    IReadOnlyList<MatchdayWeighIn> WeighIns,
    IReadOnlyList<int> WeightClassOrder,
    IReadOnlyList<TeamEncounter> Encounters);

/// <summary>
/// A team entered for one team matchday.
/// </summary>
public sealed record TeamMatchdayTeam(
    Guid Id,
    Guid TournamentId,
    Guid ClubId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// A confirmed athlete weigh-in for one team matchday.
/// </summary>
public sealed record MatchdayWeighIn(
    Guid Id,
    Guid TournamentId,
    Guid AthleteId,
    decimal WeightKg,
    DateTimeOffset ConfirmedAtUtc);

/// <summary>
/// A configured pairing of two teams on the matchday.
/// </summary>
public sealed record TeamEncounter(
    Guid Id,
    Guid TournamentId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    Guid? TatamiId,
    int DisplayOrder,
    Guid? NoShowTeamId,
    DateTimeOffset CreatedAtUtc,
    int HomeIndividualWins,
    int AwayIndividualWins,
    int HomeUnderScore,
    int AwayUnderScore,
    int HomeTeamPoints,
    int AwayTeamPoints,
    TeamEncounterOutcome? Outcome);

/// <summary>
/// An athlete assigned to an encounter leg and profile weight class.
/// </summary>
public sealed record TeamLineupEntry(
    Guid Id,
    Guid EncounterId,
    int LegNumber,
    Guid TeamId,
    int WeightClassIndex,
    Guid AthleteId);

/// <summary>
/// An assignment supplied while configuring one team encounter leg.
/// </summary>
public sealed record TeamLineupAssignment(int WeightClassIndex, Guid AthleteId);

/// <summary>
/// Result returned when a team-matchday operation is rejected by a business rule.
/// </summary>
public sealed record TeamMatchdayOperationResult(bool Succeeded, string? Code, string? Message)
{
    /// <summary>A successful operation result.</summary>
    public static TeamMatchdayOperationResult Success { get; } = new(true, null, null);
}

/// <summary>
/// One regular fight created for an encounter-leg weight class.
/// </summary>
public sealed record EncounterBout(Guid Id, Guid EncounterId, int LegNumber, int WeightClassIndex, Guid FightId);