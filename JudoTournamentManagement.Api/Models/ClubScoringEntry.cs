namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// One ranked club row in a club scoring table.
/// </summary>
public sealed record ClubScoringEntry(
    int Rank,
    bool IsSharedRank,
    Guid ClubId,
    string ClubName,
    int FirstPlaces,
    int SecondPlaces,
    int ThirdPlaces,
    int BasePoints,
    int Wins,
    int Fights,
    decimal WinRateRaw,
    decimal WinRateDisplay,
    decimal ScoreRaw,
    decimal ScoreDisplay);
