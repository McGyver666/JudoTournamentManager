namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Server UTC time response used by clients for clock offset estimation.
/// </summary>
/// <param name="ServerTimeUtc">Current server timestamp in UTC.</param>
public sealed record ServerTimeResponse(DateTimeOffset ServerTimeUtc);
