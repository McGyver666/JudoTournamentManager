using System.ComponentModel.DataAnnotations;
using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for updating a competition category.
/// </summary>
public sealed record UpdateCategoryRequest
{
    /// <summary>
    /// User-friendly display name, e.g. "U18 Männer -73 kg".
    /// </summary>
    [Required(ErrorMessage = "Der Name ist erforderlich.")]
    [MaxLength(120, ErrorMessage = "Der Name darf maximal 120 Zeichen lang sein.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Age group code, e.g. "U18" or "Senioren".
    /// </summary>
    [Required(ErrorMessage = "Die Altersklasse ist erforderlich.")]
    [MaxLength(40, ErrorMessage = "Die Altersklasse darf maximal 40 Zeichen lang sein.")]
    public string AgeGroup { get; init; } = string.Empty;

    /// <summary>
    /// Gender for this category.
    /// </summary>
    [Required(ErrorMessage = "Das Geschlecht ist erforderlich.")]
    public Gender? Gender { get; init; }

    /// <summary>
    /// Upper weight limit in kilograms; leave null for open weight.
    /// </summary>
    [Range(1.0, 999.9, ErrorMessage = "Die Gewichtsklasse muss zwischen 1 und 999,9 kg liegen.")]
    public decimal? WeightClassKg { get; init; }

    /// <summary>
    /// Optional free-text ruleset notes or flags.
    /// </summary>
    [MaxLength(500, ErrorMessage = "Die Regelwerknotizen dürfen maximal 500 Zeichen lang sein.")]
    public string? RulesetNotes { get; init; }

    /// <summary>
    /// Match duration in seconds (default 300 = 5 minutes).
    /// </summary>
    [Range(60, 3600, ErrorMessage = "Die Kampfdauer muss zwischen 60 und 3600 Sekunden liegen.")]
    public int MatchDurationSeconds { get; init; } = 300;

    /// <summary>
    /// Whether a golden score phase should be used after regular time.
    /// </summary>
    public bool GoldenScoreEnabled { get; init; }

    /// <summary>
    /// Golden score duration in seconds.
    /// </summary>
    [Range(30, 3600, ErrorMessage = "Die Golden-Score-Dauer muss zwischen 30 und 3600 Sekunden liegen.")]
    public int GoldenScoreDurationSeconds { get; init; } = 180;

    /// <summary>
    /// Minimum birth year (inclusive) for age-based auto-assignment; leave null for no lower bound.
    /// </summary>
    [Range(1900, 2100, ErrorMessage = "Das Mindest-Geburtsjahr muss zwischen 1900 und 2100 liegen.")]
    public int? MinBirthYear { get; init; }

    /// <summary>
    /// Maximum birth year (inclusive) for age-based auto-assignment; leave null for no upper bound.
    /// </summary>
    [Range(1900, 2100, ErrorMessage = "Das Höchst-Geburtsjahr muss zwischen 1900 und 2100 liegen.")]
    public int? MaxBirthYear { get; init; }
}
