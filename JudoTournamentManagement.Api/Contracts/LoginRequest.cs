using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for local user login.
/// </summary>
public sealed record LoginRequest
{
    /// <summary>User name.</summary>
    [Required]
    [MaxLength(120)]
    public string UserName { get; init; } = string.Empty;

    /// <summary>Password in plaintext (validated server-side).</summary>
    [Required]
    [MaxLength(200)]
    public string Password { get; init; } = string.Empty;
}
