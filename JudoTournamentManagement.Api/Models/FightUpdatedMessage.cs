namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Hub payload for fight updates including a server timestamp for client-side clock alignment.
/// </summary>
/// <param name="Fight">Updated fight snapshot.</param>
/// <param name="ServerNowUtc">Server time at emission in UTC.</param>
public sealed record FightUpdatedMessage(Fight Fight, DateTimeOffset ServerNowUtc);
