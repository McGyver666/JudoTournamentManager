using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for bootstrapping the first local admin account.
/// </summary>
public sealed record BootstrapAdminRequest
{
    /// <summary>Admin user name.</summary>
    [Required]
    [MaxLength(120)]
    public string UserName { get; init; } = string.Empty;

    /// <summary>Admin password in plaintext (hashed server-side only).</summary>
    [Required]
    [MinLength(12)]
    [MaxLength(200)]
    public string Password { get; init; } = string.Empty;
}
