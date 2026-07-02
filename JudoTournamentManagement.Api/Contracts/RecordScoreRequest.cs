using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for recording the current score and penalties of a fight.
/// </summary>
public sealed record RecordScoreRequest
{
    /// <summary>Accumulated score for the White athlete.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Die Wertung darf nicht negativ sein.")]
    public int WhiteScore { get; init; }

    /// <summary>Accumulated score for the Blue athlete.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Die Wertung darf nicht negativ sein.")]
    public int BlueScore { get; init; }

    /// <summary>Number of penalties (Shido) for the White athlete.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Die Anzahl der Strafen darf nicht negativ sein.")]
    public int WhitePenalties { get; init; }

    /// <summary>Number of penalties (Shido) for the Blue athlete.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Die Anzahl der Strafen darf nicht negativ sein.")]
    public int BluePenalties { get; init; }
}

/// <summary>
/// Score type adjustments that can be applied to a fight.
/// </summary>
public enum ScoreType
{
    Ippon,
    WazaAri,
    Yuko,
    Shido
}

/// <summary>
/// Request payload for adding or removing a single score increment.
/// </summary>
public sealed record AdjustScoreRequest
{
    /// <summary>Target side.</summary>
    public required string Side { get; init; }

    /// <summary>Score type to adjust.</summary>
    public required ScoreType ScoreType { get; init; }

    /// <summary>Delta to apply, typically +1 or -1.</summary>
    public int Delta { get; init; } = 1;
}

/// <summary>
/// Request payload for osae-komi controls.
/// </summary>
public sealed record OsaeKomiRequest
{
    /// <summary>Target side.</summary>
    public required string Side { get; init; }
}
