namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Login outcome states used by the authentication service.
/// </summary>
public enum LoginStatus
{
    Success,
    InvalidCredentials,
    Locked,
    Inactive
}
