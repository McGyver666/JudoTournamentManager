using System.ComponentModel.DataAnnotations;
using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Contracts;

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
