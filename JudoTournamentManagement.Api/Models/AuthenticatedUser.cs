namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Authenticated local user identity resolved from a valid bearer token.
/// </summary>
public sealed record AuthenticatedUser(Guid UserId, string UserName, string Role);
