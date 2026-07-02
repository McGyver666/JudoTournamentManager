namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Represents an athlete's standing within a round-robin pool or the full round-robin bracket.
/// </summary>
/// <param name="AthleteId">Athlete identifier.</param>
/// <param name="AthleteName">Full name of the athlete (last name, first name).</param>
/// <param name="ClubName">Name of the athlete's club.</param>
/// <param name="PoolNumber">Pool/group number; 0 for pure round-robin with no pools.</param>
/// <param name="Rank">1-based placement within the pool.</param>
/// <param name="Wins">Number of fights won.</param>
/// <param name="WazaAriScored">Total waza-ari scores recorded across all fights.</param>
/// <param name="YukoScored">Total yuko scores recorded across all fights.</param>
/// <param name="Shidos">Total shido penalties received across all fights.</param>
public sealed record RoundRobinStanding(
    Guid AthleteId,
    string AthleteName,
    string ClubName,
    int PoolNumber,
    int Rank,
    int Wins,
    int WazaAriScored,
    int YukoScored,
    int Shidos);
