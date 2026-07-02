namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Represents a competition category (Alters-/Gewichtsklasse) within a tournament.
/// </summary>
/// <param name="Id">Unique category identifier.</param>
/// <param name="TournamentId">Owning tournament identifier.</param>
/// <param name="Name">User-friendly display name, e.g. "U18 Männer -73 kg".</param>
/// <param name="AgeGroup">Age group code, e.g. "U18" or "Senioren".</param>
/// <param name="Gender">Gender for this category.</param>
/// <param name="WeightClassKg">Upper weight limit in kilograms; null means open weight.</param>
/// <param name="MinBirthYear">Minimum birth year (inclusive) for age-based matching; null means no lower bound.</param>
/// <param name="MaxBirthYear">Maximum birth year (inclusive) for age-based matching; null means no upper bound.</param>
/// <param name="RulesetNotes">Optional free-text ruleset notes or flags.</param>
/// <param name="MatchDurationSeconds">Match duration in seconds (default 300 = 5 minutes).</param>
/// <param name="GoldenScoreEnabled">Whether a golden score phase is enabled after regular time expires.</param>
/// <param name="GoldenScoreDurationSeconds">Golden score duration in seconds (default 180 = 3 minutes).</param>
/// <param name="DrawFormat">Bracket format used when generating the draw; null before any draw is generated.</param>
/// <param name="IsLocked">True once the first real fight has started; blocks further changes.</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
/// <param name="UpdatedAtUtc">Last update timestamp in UTC.</param>
public sealed record Category(
    Guid Id,
    Guid TournamentId,
    string Name,
    string AgeGroup,
    Gender Gender,
    decimal? WeightClassKg,
    int? MinBirthYear,
    int? MaxBirthYear,
    string? RulesetNotes,
    int MatchDurationSeconds,
    bool GoldenScoreEnabled,
    int GoldenScoreDurationSeconds,
    BracketFormat? DrawFormat,
    bool IsLocked,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
