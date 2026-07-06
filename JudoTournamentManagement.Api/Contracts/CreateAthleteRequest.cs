using System.ComponentModel.DataAnnotations;
using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for creating an athlete.
/// </summary>
public sealed record CreateAthleteRequest
{
    /// <summary>
    /// Club the athlete competes for.
    /// </summary>
    [Required(ErrorMessage = "Der Verein ist erforderlich.")]
    public Guid ClubId { get; init; }

    /// <summary>
    /// Given name.
    /// </summary>
    [Required(ErrorMessage = "Der Vorname ist erforderlich.")]
    [MaxLength(60, ErrorMessage = "Der Vorname darf maximal 60 Zeichen lang sein.")]
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Family name.
    /// </summary>
    [Required(ErrorMessage = "Der Nachname ist erforderlich.")]
    [MaxLength(60, ErrorMessage = "Der Nachname darf maximal 60 Zeichen lang sein.")]
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Year of birth.
    /// </summary>
    [Required(ErrorMessage = "Das Geburtsjahr ist erforderlich.")]
    [Range(1940, 2030, ErrorMessage = "Das Geburtsjahr muss zwischen 1940 und 2030 liegen.")]
    public int? BirthYear { get; init; }

    /// <summary>
    /// Gender.
    /// </summary>
    [Required(ErrorMessage = "Das Geschlecht ist erforderlich.")]
    public Gender? Gender { get; init; }

    /// <summary>
    /// Optional federation license identifier.
    /// </summary>
    [MaxLength(40, ErrorMessage = "Die Lizenznummer darf maximal 40 Zeichen lang sein.")]
    public string? LicenseId { get; init; }

    /// <summary>
    /// Optional athlete body weight in kilograms.
    /// </summary>
    [Range(1.0, 300.0, ErrorMessage = "Das Gewicht muss zwischen 1 und 300 kg liegen.")]
    public decimal? WeightKg { get; init; }

    /// <summary>
    /// Belt grade as numeric scale (1=9. Kyu ... 9=1. Kyu, 10=1. Dan ... 14=5. Dan).
    /// </summary>
    [Required(ErrorMessage = "Der Gürtelgrad ist erforderlich.")]
    [Range(1, 14, ErrorMessage = "Der Gürtelgrad muss zwischen 1 und 14 liegen.")]
    public int? Grade { get; init; }
}
