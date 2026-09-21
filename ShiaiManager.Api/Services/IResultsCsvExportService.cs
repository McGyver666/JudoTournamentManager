using ShiaiManager.Api.Models;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Creates the CSV projection used for tournament result certificates.
/// </summary>
public interface IResultsCsvExportService
{
    /// <summary>
    /// Creates a deterministic CSV export from the current category rankings.
    /// </summary>
    Task<ResultsCsvExport> CreateAsync(Tournament tournament, CancellationToken cancellationToken);
}