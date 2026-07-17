using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for creating a club.
/// </summary>
public sealed record CreateClubRequest
{
    /// <summary>
    /// Club display name; must be unique within the tournament.
    /// </summary>
    [Required(ErrorMessage = "Der Vereinsname ist erforderlich.")]
    [MaxLength(120, ErrorMessage = "Der Vereinsname darf maximal 120 Zeichen lang sein.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional contact person name (first and last name).</summary>
    [MaxLength(120, ErrorMessage = "Der Name des Ansprechpartners darf maximal 120 Zeichen lang sein.")]
    public string? ContactName { get; init; }

    /// <summary>Optional contact e-mail address.</summary>
    [MaxLength(254, ErrorMessage = "Die E-Mail-Adresse darf maximal 254 Zeichen lang sein.")]
    [EmailAddress(ErrorMessage = "Die E-Mail-Adresse ist ungültig.")]
    public string? ContactEmail { get; init; }

    /// <summary>Optional contact phone number.</summary>
    [MaxLength(50, ErrorMessage = "Die Telefonnummer darf maximal 50 Zeichen lang sein.")]
    public string? ContactPhone { get; init; }
}
