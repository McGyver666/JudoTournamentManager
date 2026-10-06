using System.ComponentModel.DataAnnotations;

namespace ShiaiManager.Api.Contracts;

/// <summary>
/// Request body for selecting or clearing a registration's start age group.
/// </summary>
public sealed record UpdateRegistrationStartAgeGroupRequest
{
    /// <summary>Preset age-group label, or null to use the natural age group.</summary>
    [MaxLength(40, ErrorMessage = "Die Startaltersklasse darf maximal 40 Zeichen lang sein.")]
    public string? StartAgeGroup { get; init; }
}