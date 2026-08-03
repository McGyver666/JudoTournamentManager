namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Data-minimized public projection of an athlete for the anonymous match-list
/// view. Deliberately excludes personal data that is not rendered on the list
/// (birth year, gender, license/pass number, weight, grade, contact data).
/// </summary>
/// <param name="Id">Unique athlete identifier.</param>
/// <param name="ClubId">Club the athlete competes for.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
public sealed record PublicAthlete(
    Guid Id,
    Guid ClubId,
    string FirstName,
    string LastName);
