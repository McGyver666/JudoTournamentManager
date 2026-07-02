using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for creating a local user account.
/// </summary>
public sealed record CreateUserRequest
{
    /// <summary>Login/display user name.</summary>
    [Required]
    [MaxLength(120)]
    public string UserName { get; init; } = string.Empty;

    /// <summary>Assigned role (Admin, Operator, Display).</summary>
    [Required]
    [MaxLength(40)]
    public string Role { get; init; } = string.Empty;

    /// <summary>Initial plaintext password (hashed server-side).</summary>
    [Required]
    [MinLength(12)]
    [MaxLength(200)]
    public string Password { get; init; } = string.Empty;
}
