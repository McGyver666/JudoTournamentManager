using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Hubs;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for bracket generation and fight management within a category.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/categories/{categoryId:guid}")]
public sealed class FightsController : ControllerBase
{
    private readonly IBracketService _bracketService;
    private readonly IFightsStore _fightsStore;
    private readonly ICategoriesStore _categoriesStore;
    private readonly ITournamentStore _tournamentStore;
    private readonly IAuditLogService _auditLog;
    private readonly IHubContext<TournamentHub> _hub;
    private readonly IRankingService _rankingService;

    /// <summary>Initializes a new controller instance.</summary>
    public FightsController(
        IBracketService bracketService,
        IFightsStore fightsStore,
        ICategoriesStore categoriesStore,
        ITournamentStore tournamentStore,
        IAuditLogService auditLog,
        IHubContext<TournamentHub> hub,
        IRankingService rankingService)
    {
        ArgumentNullException.ThrowIfNull(bracketService);
        ArgumentNullException.ThrowIfNull(fightsStore);
        ArgumentNullException.ThrowIfNull(categoriesStore);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(rankingService);
        _bracketService = bracketService;
        _fightsStore = fightsStore;
        _categoriesStore = categoriesStore;
        _tournamentStore = tournamentStore;
        _auditLog = auditLog;
        _hub = hub;
        _rankingService = rankingService;
    }

    /// <summary>
    /// Returns all fights for a category ordered by bracket type, round, and fight number.
    /// </summary>
    [Authorize]
    [HttpGet("fights")]
    [ProducesResponseType(typeof(IReadOnlyList<Fight>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Fight>>> GetFightsAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!await CategoryBelongsToTournamentAsync(tournamentId, categoryId, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await _fightsStore.GetAllAsync(tournamentId, categoryId, cancellationToken));
    }

    /// <summary>
    /// Generates the draw for a category. Replaces any existing bracket.
    /// Requires at least 2 registered athletes.
    /// Category remains editable until the first real fight is started.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("draw")]
    [ProducesResponseType(typeof(IReadOnlyList<Fight>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Fight>>> GenerateDrawAsync(
        Guid tournamentId,
        Guid categoryId,
        [FromBody] GenerateDrawRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Format is null)
        {
            ModelState.AddModelError(nameof(request.Format), "Das Auslosungsformat ist erforderlich.");
            return ValidationProblem(ModelState);
        }

        if (!await CategoryBelongsToTournamentAsync(tournamentId, categoryId, cancellationToken))
        {
            return NotFound();
        }

        IReadOnlyList<Fight> fights;
        try
        {
            fights = await _bracketService.GenerateAsync(
                tournamentId, categoryId, request.Format.Value, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }

        var user = User.Identity?.Name ?? "unbekannt";
        await _auditLog.LogAsync(
            tournamentId, user, "DrawGenerated", "Category", categoryId,
            $"Format={request.Format.Value}; Fights={fights.Count}", cancellationToken);

        _ = _hub.Clients.Group(tournamentId.ToString())
            .SendAsync("CategoryFightsUpdated",
                new { tournamentId, categoryId },
                CancellationToken.None);

        return CreatedAtAction(
            nameof(GetFightsAsync),
            new { tournamentId, categoryId },
            fights);
    }

    /// <summary>
    /// Swaps two athletes' positions in the bracket.
    /// Returns 409 Conflict when the bracket is locked (a fight has started or completed)
    /// or when either athlete is not in the bracket.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost("swap")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SwapAthletesAsync(
        Guid tournamentId,
        Guid categoryId,
        [FromBody] SwapAthletesRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CategoryBelongsToTournamentAsync(tournamentId, categoryId, cancellationToken))
        {
            return NotFound();
        }

        var result = await _bracketService.SwapAthletesAsync(
            categoryId, request.AthleteId1, request.AthleteId2, cancellationToken);

        return result switch
        {
            SwapResult.Success => NoContent(),
            SwapResult.BracketLocked => Conflict(new ProblemDetails
            {
                Title = "Auslosung ist gesperrt (Kampf bereits gestartet).",
                Detail = "Der erste reale Kampf wurde bereits gestartet oder abgeschlossen. Die Auslosung kann nicht mehr veraendert werden.",
                Status = StatusCodes.Status409Conflict
            }),
            SwapResult.AthleteNotInBracket => Conflict(new ProblemDetails
            {
                Title = "Athleten nicht in der Auslosung.",
                Detail = "Einer oder beide Athleten sind nicht in der Auslosung dieser Kategorie vorhanden.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private async Task<bool> CategoryBelongsToTournamentAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null) return false;

        var category = await _categoriesStore.GetByIdAsync(categoryId, cancellationToken);
        return category is not null && category.TournamentId == tournamentId;
    }

    /// <summary>
    /// Returns provisional rankings (1st/2nd/3rd) for a category based on current bracket state.
    /// </summary>
    [Authorize]
    [HttpGet("rankings")]
    [ProducesResponseType(typeof(IReadOnlyList<RankingEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RankingEntry>>> GetRankingsAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!await CategoryBelongsToTournamentAsync(tournamentId, categoryId, cancellationToken))
        {
            return NotFound();
        }

        var rankings = await _rankingService.GetCategoryRankingsAsync(tournamentId, categoryId, cancellationToken);
        return Ok(rankings);
    }

    /// <summary>
    /// Returns round-robin standings for a category.
    /// Tie-break order: wins → waza-ari → yuko → fewest shidos.
    /// Returns an empty array for non-round-robin categories.
    /// </summary>
    [Authorize]
    [HttpGet("standings")]
    [ProducesResponseType(typeof(IReadOnlyList<RoundRobinStanding>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RoundRobinStanding>>> GetStandingsAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!await CategoryBelongsToTournamentAsync(tournamentId, categoryId, cancellationToken))
        {
            return NotFound();
        }

        var standings = await _rankingService.GetRoundRobinStandingsAsync(tournamentId, categoryId, cancellationToken);
        return Ok(standings);
    }
}
