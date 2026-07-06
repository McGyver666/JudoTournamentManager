namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Configurable preset row used by the standard-class category generation assistant.
/// Age limits are stored as age offsets relative to tournament year so birth years
/// automatically adjust when the tournament year changes.
/// </summary>
/// <param name="Id">Unique preset identifier.</param>
/// <param name="TournamentId">Owning tournament identifier.</param>
/// <param name="AgeGroup">Age-group label, e.g. "U13", "Frauen".</param>
/// <param name="Gender">Gender for this preset row.</param>
/// <param name="MaxAgeYears">
/// Maximum athlete age (inclusive). Used to compute <see cref="MinBirthYear"/>:
/// <c>tournamentYear - MaxAgeYears</c>. Null means no oldest-athlete limit.
/// </param>
/// <param name="MinAgeYears">
/// Minimum athlete age (inclusive). Used to compute <see cref="MaxBirthYear"/>:
/// <c>tournamentYear - MinAgeYears</c>. Null means no youngest-athlete limit.
/// </param>
/// <param name="MinBirthYear">
/// Computed minimum birth year (oldest athletes allowed). Null when <paramref name="MaxAgeYears"/> is null.
/// </param>
/// <param name="MaxBirthYear">
/// Computed maximum birth year (youngest athletes allowed). Null when <paramref name="MinAgeYears"/> is null.
/// </param>
/// <param name="DefaultMatchDurationSeconds">Default match duration in seconds for this age group.</param>
/// <param name="WeightClassLimitsKg">
/// Ordered list of weight-class upper limits in kg. A trailing <c>null</c> element represents the open/heavy class.
/// </param>
/// <param name="SortOrder">Display sort order within the tournament's preset list.</param>
public sealed record TournamentCategoryPreset(
    Guid Id,
    Guid TournamentId,
    string AgeGroup,
    Gender Gender,
    int? MaxAgeYears,
    int? MinAgeYears,
    int? MinBirthYear,
    int? MaxBirthYear,
    int DefaultMatchDurationSeconds,
    IReadOnlyList<decimal?> WeightClassLimitsKg,
    int SortOrder);
