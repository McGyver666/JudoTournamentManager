namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for toggling local user active state.
/// </summary>
public sealed record SetUserActiveRequest(bool IsActive);
