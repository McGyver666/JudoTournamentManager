using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for assigning a category to a registered athlete.
/// </summary>
public sealed record AssignCategoryRequest
{
    /// <summary>
    /// Category to assign to the athlete.
    /// </summary>
    [Required(ErrorMessage = "Die Kategorie ist erforderlich.")]
    public Guid CategoryId { get; init; }
}
