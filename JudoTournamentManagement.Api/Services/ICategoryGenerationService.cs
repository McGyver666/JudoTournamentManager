using JudoTournamentManagement.Api.Contracts;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Generates tournament categories from assistant input with preview/apply workflow.
/// </summary>
public interface ICategoryGenerationService
{
    /// <summary>
    /// Computes category proposals without persisting changes.
    /// </summary>
    Task<CategoryGenerationPreviewResponse> PreviewAsync(
        Guid tournamentId,
        GenerateCategoriesRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies generation by optionally replacing unlocked generated categories and creating proposals.
    /// </summary>
    Task<CategoryGenerationApplyResponse> ApplyAsync(
        Guid tournamentId,
        GenerateCategoriesRequest request,
        CancellationToken cancellationToken);
}
