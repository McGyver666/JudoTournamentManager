namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Enriched registration view including athlete and category details; used for API responses and CSV export.
/// </summary>
/// <param name="Id">Unique registration identifier.</param>
/// <param name="TournamentId">Tournament this registration belongs to.</param>
/// <param name="AthleteId">Registered athlete identifier.</param>
/// <param name="AthleteLastName">Athlete's family name.</param>
/// <param name="AthleteFirstName">Athlete's given name.</param>
/// <param name="AthleteBirthYear">Athlete's year of birth.</param>
/// <param name="AthleteGender">Athlete's gender.</param>
/// <param name="AthleteClubName">Athlete's club name.</param>
/// <param name="AthleteLicenseId">Optional athlete license identifier.</param>
/// <param name="CategoryId">Category identifier.</param>
/// <param name="CategoryName">Category display name.</param>
/// <param name="CategoryAgeGroup">Category age group code.</param>
/// <param name="CategoryGender">Category gender.</param>
/// <param name="CategoryWeightClassKg">Category weight limit in kg; null means open weight.</param>
/// <param name="CategoryId">Category identifier (null if not yet assigned).</param>
/// <param name="AthleteWeightKg">Athlete's weight in kg captured at registration (weight-in).</param>
/// <param name="LicenseConfirmed">Whether the athlete's license was verified at registration.</param>
public sealed record RegistrationDetail(
    Guid Id,
    Guid TournamentId,
    Guid AthleteId,
    string AthleteLastName,
    string AthleteFirstName,
    int AthleteBirthYear,
    Gender AthleteGender,
    string AthleteClubName,
    string? AthleteLicenseId,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryAgeGroup,
    Gender? CategoryGender,
    decimal? CategoryWeightClassKg,
    decimal? AthleteWeightKg,
    bool LicenseConfirmed,
    DateTimeOffset CreatedAtUtc);
