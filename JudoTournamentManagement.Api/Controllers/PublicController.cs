using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// Read-only, data-minimized endpoints that back the public match-list view.
/// Accessible to authenticated operators (Admin, Operator, Display) and to
/// anonymous guests holding a valid share token for the requested tournament.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/public")]
[Authorize(Roles = "Admin,Operator,Display,Guest")]
[EnableRateLimiting("PublicPolicy")]
public sealed class PublicController : ControllerBase
{
    private readonly ITournamentStore _tournamentStore;
    private readonly IAthletesStore _athletesStore;
    private readonly IClubsStore _clubsStore;
    private readonly ICategoriesStore _categoriesStore;
    private readonly IFightsStore _fightsStore;
    private readonly IRankingService _rankingService;

    /// <summary>Initializes a new controller instance.</summary>
    public PublicController(
        ITournamentStore tournamentStore,
        IAthletesStore athletesStore,
        IClubsStore clubsStore,
        ICategoriesStore categoriesStore,
        IFightsStore fightsStore,
        IRankingService rankingService)
    {
        ArgumentNullException.ThrowIfNull(tournamentStore);
        ArgumentNullException.ThrowIfNull(athletesStore);
        ArgumentNullException.ThrowIfNull(clubsStore);
        ArgumentNullException.ThrowIfNull(categoriesStore);
        ArgumentNullException.ThrowIfNull(fightsStore);
        ArgumentNullException.ThrowIfNull(rankingService);
        _tournamentStore = tournamentStore;
        _athletesStore = athletesStore;
        _clubsStore = clubsStore;
        _categoriesStore = categoriesStore;
        _fightsStore = fightsStore;
        _rankingService = rankingService;
    }

    /// <summary>
    /// Returns the data-minimized athlete list (id, club, name only) for the tournament.
    /// </summary>
    [HttpGet("athletes")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicAthlete>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PublicAthlete>>> GetAthletesAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!IsGuestScopeAllowed(tournamentId))
        {
            return Forbid();
        }

        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var athletes = await _athletesStore.GetAllAsync(tournamentId, cancellationToken);
        var result = athletes
            .Select(a => new PublicAthlete(a.Id, a.ClubId, a.FirstName, a.LastName))
            .ToList();
        return Ok(result);
    }

    /// <summary>
    /// Returns the data-minimized club list (id, name only) for the tournament.
    /// </summary>
    [HttpGet("clubs")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicClub>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PublicClub>>> GetClubsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!IsGuestScopeAllowed(tournamentId))
        {
            return Forbid();
        }

        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var clubs = await _clubsStore.GetAllAsync(tournamentId, cancellationToken);
        var result = clubs.Select(c => new PublicClub(c.Id, c.Name)).ToList();
        return Ok(result);
    }

    /// <summary>
    /// Returns the categories for the tournament.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<Category>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Category>>> GetCategoriesAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!IsGuestScopeAllowed(tournamentId))
        {
            return Forbid();
        }

        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await _categoriesStore.GetAllAsync(tournamentId, cancellationToken));
    }

    /// <summary>
    /// Returns the fights of a category for the tournament.
    /// </summary>
    [HttpGet("categories/{categoryId:guid}/fights")]
    [ProducesResponseType(typeof(IReadOnlyList<Fight>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Fight>>> GetFightsAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!IsGuestScopeAllowed(tournamentId))
        {
            return Forbid();
        }

        if (!await CategoryBelongsToTournamentAsync(tournamentId, categoryId, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await _fightsStore.GetAllAsync(tournamentId, categoryId, cancellationToken));
    }

    /// <summary>
    /// Returns the round-robin standings of a category for the tournament.
    /// </summary>
    [HttpGet("categories/{categoryId:guid}/standings")]
    [ProducesResponseType(typeof(IReadOnlyList<RoundRobinStanding>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RoundRobinStanding>>> GetStandingsAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!IsGuestScopeAllowed(tournamentId))
        {
            return Forbid();
        }

        if (!await CategoryBelongsToTournamentAsync(tournamentId, categoryId, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await _rankingService.GetRoundRobinStandingsAsync(tournamentId, categoryId, cancellationToken));
    }

    /// <summary>
    /// Returns the tournament header (name, accent color, rules) for the public
    /// match-list view. Contains no personal data.
    /// </summary>
    [HttpGet("tournament")]
    [ProducesResponseType(typeof(Tournament), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Tournament>> GetTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!IsGuestScopeAllowed(tournamentId))
        {
            return Forbid();
        }

        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
        {
            return NotFound();
        }

        return Ok(tournament);
    }

    /// <summary>
    /// Guests may only read the tournament their token is scoped to. Operator roles
    /// are unrestricted.
    /// </summary>
    private bool IsGuestScopeAllowed(Guid tournamentId)
    {
        if (!User.IsInRole(IGuestShareService.GuestRole))
        {
            return true;
        }

        var scoped = User.FindFirst(IGuestShareService.TournamentClaimType)?.Value;
        return Guid.TryParse(scoped, out var scopedId) && scopedId == tournamentId;
    }

    private async Task<bool> TournamentExistsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken) is not null;
    }

    private async Task<bool> CategoryBelongsToTournamentAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var category = await _categoriesStore.GetByIdAsync(categoryId, cancellationToken);
        return category is not null && category.TournamentId == tournamentId;
    }
}
