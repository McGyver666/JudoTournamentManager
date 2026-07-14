namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Enriched registration view including athlete, category, and license verification details.
/// Used for API responses and CSV export.
/// </summary>
/// <param name="Id">Unique registration identifier.</param>
/// <param name="TournamentId">Tournament this registration belongs to.</param>
/// <param name="AthleteId">Registered athlete identifier.</param>
/// <param name="AthleteLastName">Athlete's family name.</param>
/// <param name="AthleteFirstName">Athlete's given name.</param>
/// <param name="AthleteBirthYear">Athlete's year of birth.</param>
/// <param name="AthleteGender">Athlete's gender.</param>
/// <param name="AthleteClubName">Athlete's club name.</param>
/// <param name="CategoryId">Category identifier (null if not yet assigned).</param>
/// <param name="CategoryName">Category display name.</param>
/// <param name="CategoryAgeGroup">Category age group code.</param>
/// <param name="CategoryGender">Category gender.</param>
/// <param name="CategoryWeightClassKg">Category weight limit in kg; null means open weight.</param>
/// <param name="AthleteWeightKg">Athlete's weight in kg captured at registration (weight-in).</param>
/// <param name="LicenseConfirmed">Whether the athlete's license was verified at registration.</param>
/// <param name="CreatedAtUtc">Registration creation timestamp in UTC.</param>
/// <param name="LicenseNumber">License number from DokuMe QR code (NO claim), if available.</param>
/// <param name="PassExpiryDate">License expiry date from DokuMe QR code (exp claim), if available.</param>
/// <param name="LicenseCheckPassed">True if DokuMe license check passed all validation rules; null if not checked.</param>
/// <param name="LicenseVerifiedAtUtc">Timestamp of license verification or override; null if not checked.</param>
/// <param name="LicenseVerifiedByUser">Operator who performed the license check; null if not checked.</param>
public sealed record RegistrationDetail(
    Guid Id,
    Guid TournamentId,
    Guid AthleteId,
    string AthleteLastName,
    string AthleteFirstName,
    int AthleteBirthYear,
    Gender AthleteGender,
    string AthleteClubName,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryAgeGroup,
    Gender? CategoryGender,
    decimal? CategoryWeightClassKg,
    decimal? AthleteWeightKg,
    bool LicenseConfirmed,
    DateTimeOffset CreatedAtUtc,
    string? LicenseNumber = null,
    DateOnly? PassExpiryDate = null,
    bool? LicenseCheckPassed = null,
    DateTimeOffset? LicenseVerifiedAtUtc = null,
    string? LicenseVerifiedByUser = null);
