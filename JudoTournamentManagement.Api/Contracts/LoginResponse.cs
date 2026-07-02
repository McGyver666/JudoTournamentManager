namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Response returned on successful login.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string UserName,
    string Role);
