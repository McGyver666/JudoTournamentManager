namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Represents an athlete registered for a tournament.
/// </summary>
/// <param name="Id">Unique athlete identifier.</param>
/// <param name="TournamentId">Owning tournament identifier.</param>
/// <param name="ClubId">Club the athlete competes for.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="BirthYear">Year of birth.</param>
/// <param name="Gender">Athlete's gender.</param>
/// <param name="LicenseId">Optional federation license identifier.</param>
/// <param name="WeightKg">Optional athlete body weight in kilograms.</param>
/// <param name="Grade">Belt grade as numeric scale (1=9. Kyu ... 9=1. Kyu, 10=1. Dan ... 14=5. Dan).</param>
/// <param name="LastFightDurationSeconds">Duration in seconds of the athlete's most recent completed fight.</param>
/// <param name="LastFightEndedAtUtc">UTC timestamp when the athlete's most recent completed fight ended.</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
/// <param name="UpdatedAtUtc">Last update timestamp in UTC.</param>
public sealed record Athlete(
    Guid Id,
    Guid TournamentId,
    Guid ClubId,
    string FirstName,
    string LastName,
    int BirthYear,
    Gender Gender,
    string? LicenseId,
    decimal? WeightKg,
    int Grade,
    int? LastFightDurationSeconds,
    DateTimeOffset? LastFightEndedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
