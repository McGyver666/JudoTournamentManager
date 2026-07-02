using System.ComponentModel.DataAnnotations;
using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for generating the draw for a category.
/// </summary>
public sealed record GenerateDrawRequest
{
    /// <summary>
    /// Bracket format to use for this category.
    /// </summary>
    [Required(ErrorMessage = "Das Auslosungsformat ist erforderlich.")]
    public BracketFormat? Format { get; init; }
}
