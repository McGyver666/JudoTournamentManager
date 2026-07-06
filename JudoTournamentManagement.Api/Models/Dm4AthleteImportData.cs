namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Parsed DM4 athlete import payload.
/// </summary>
public sealed record Dm4AthleteImportData(
    string ClubName,
    Gender Gender,
    IReadOnlyList<Dm4AthleteImportRow> Athletes);

/// <summary>
/// Parsed athlete row from a DM4 [Teilnehmer] section.
/// </summary>
public sealed record Dm4AthleteImportRow(
    string LastName,
    string FirstName,
    int Grade,
    decimal? WeightKg,
    int BirthYear);
