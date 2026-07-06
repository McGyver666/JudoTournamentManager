namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Domain input for creating an athlete as part of a bulk import.
/// </summary>
public sealed record AthleteImportItem(
    Guid ClubId,
    string FirstName,
    string LastName,
    int BirthYear,
    Gender Gender,
    string? LicenseId,
    decimal? WeightKg,
    int Grade);