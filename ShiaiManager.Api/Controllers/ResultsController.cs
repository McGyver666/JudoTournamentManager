using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShiaiManager.Api.Controllers;

/// <summary>
/// API endpoints for tournament results: medal table (G-03).
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}")]
public sealed class ResultsController : ControllerBase
{
    private readonly IRankingService _rankingService;
    private readonly ITournamentStore _tournamentStore;
    private readonly IResultsCsvExportService _resultsCsvExportService;

    /// <summary>Initializes a new controller instance.</summary>
    public ResultsController(
        IRankingService rankingService,
        ITournamentStore tournamentStore,
        IResultsCsvExportService resultsCsvExportService)
    {
        ArgumentNullException.ThrowIfNull(rankingService);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        ArgumentNullException.ThrowIfNull(resultsCsvExportService);
        _rankingService = rankingService;
        _tournamentStore = tournamentStore;
        _resultsCsvExportService = resultsCsvExportService;
    }

    /// <summary>
    /// Returns the medal table for a tournament, sorted by gold, silver, bronze, then club name.
    /// </summary>
    [Authorize]
    [HttpGet("medal-table")]
    [ProducesResponseType(typeof(IReadOnlyList<MedalEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MedalEntry>>> GetMedalTableAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        var table = await _rankingService.GetMedalTableAsync(tournamentId, cancellationToken);
        return Ok(table);
    }

    /// <summary>
    /// Exports all currently determined individual category placements as a CSV file.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpGet("results/export")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportCsvAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        var export = await _resultsCsvExportService.CreateAsync(tournament, cancellationToken);
        return File(export.Content, "text/csv", export.FileName);
    }

    /// <summary>
    /// Returns club scoring grouped by age group.
    /// </summary>
    [Authorize]
    [HttpGet("club-scoring/age-groups")]
    [ProducesResponseType(typeof(AgeGroupClubScoringResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgeGroupClubScoringResponse>> GetAgeGroupClubScoringAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        var scoring = await _rankingService.GetAgeGroupClubScoringAsync(tournamentId, cancellationToken);
        return Ok(scoring);
    }

    /// <summary>
    /// Returns turnier-wide (global) club scoring.
    /// </summary>
    [Authorize]
    [HttpGet("club-scoring/global")]
    [ProducesResponseType(typeof(GlobalClubScoringResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GlobalClubScoringResponse>> GetGlobalClubScoringAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        var scoring = await _rankingService.GetGlobalClubScoringAsync(tournamentId, cancellationToken);
        return Ok(scoring);
    }
}
