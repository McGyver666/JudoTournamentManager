using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for admin-triggered password reset.
/// </summary>
public sealed record ResetUserPasswordRequest
{
    /// <summary>New plaintext password (hashed server-side).</summary>
    [Required]
    [MinLength(12)]
    [MaxLength(200)]
    public string NewPassword { get; init; } = string.Empty;
}
