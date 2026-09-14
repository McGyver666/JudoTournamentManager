using System.ComponentModel.DataAnnotations;

namespace ShiaiManager.Api.Contracts;

/// <summary>
/// Request payload for changing the authenticated user's password.
/// </summary>
public sealed record ChangePasswordRequest
{
    /// <summary>Current plaintext password, verified server-side.</summary>
    [Required]
    [MaxLength(200)]
    public string CurrentPassword { get; init; } = string.Empty;

    /// <summary>New plaintext password, hashed server-side.</summary>
    [Required]
    [MinLength(12)]
    [MaxLength(200)]
    public string NewPassword { get; init; } = string.Empty;
}
