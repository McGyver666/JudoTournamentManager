using System.ComponentModel.DataAnnotations;
using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Read-only representation of one category preset row (returned by GET).
/// Birth years are computed from the tournament date and included for display.
/// </summary>
public sealed record CategoryPresetResponse(
    Guid Id,
    string AgeGroup,
    Gender Gender,
    int? MaxAgeYears,
    int? MinAgeYears,
    int? MinBirthYear,
    int? MaxBirthYear,
    int DefaultMatchDurationSeconds,
    IReadOnlyList<decimal?> WeightClassLimitsKg,
    int SortOrder);

/// <summary>
/// Request body for a single preset row when updating presets.
/// </summary>
public sealed record CategoryPresetItemRequest
{
    /// <summary>Age-group label, e.g. "U13", "Frauen".</summary>
    [Required]
    [MaxLength(40)]
    public string AgeGroup { get; init; } = string.Empty;

    /// <summary>Gender for this preset row.</summary>
    [Required]
    public Gender Gender { get; init; }

    /// <summary>
    /// Maximum athlete age (inclusive). MinBirthYear = tournamentYear - MaxAgeYears.
    /// Null means no oldest-athlete limit.
    /// </summary>
    [Range(1, 120)]
    public int? MaxAgeYears { get; init; }

    /// <summary>
    /// Minimum athlete age (inclusive). MaxBirthYear = tournamentYear - MinAgeYears.
    /// Null means no youngest-athlete limit.
    /// </summary>
    [Range(1, 120)]
    public int? MinAgeYears { get; init; }

    /// <summary>Default match duration in seconds.</summary>
    [Range(30, 3600)]
    public int DefaultMatchDurationSeconds { get; init; } = 240;

    /// <summary>
    /// Ordered weight-class upper limits in kg. A null element represents the open/heavy class.
    /// </summary>
    [Required]
    public IReadOnlyList<decimal?> WeightClassLimitsKg { get; init; } = [];
}

/// <summary>
/// Request body for a full preset replacement (PUT).
/// </summary>
public sealed record UpdateCategoryPresetsRequest
{
    /// <summary>Complete list of preset rows to persist (replaces existing rows).</summary>
    [Required]
    public IReadOnlyList<CategoryPresetItemRequest> Presets { get; init; } = [];
}
