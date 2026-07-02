using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for creating a tatami.
/// </summary>
public sealed record CreateTatamiRequest
{
    /// <summary>
    /// Display name, e.g. "Tatami 1".
    /// </summary>
    [Required(ErrorMessage = "Der Name ist erforderlich.")]
    [MaxLength(80, ErrorMessage = "Der Name darf maximal 80 Zeichen lang sein.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Position in display/queue sequence (0-based). If omitted the tatami is appended last.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Die Anzeigereihenfolge muss eine nicht-negative Zahl sein.")]
    public int? DisplayOrder { get; init; }
}
