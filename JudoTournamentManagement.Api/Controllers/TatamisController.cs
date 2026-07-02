using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for tatami (competition area) management within a tournament.
/// </summary>
[ApiController]
[Route("api/tournaments/{tournamentId:guid}/tatamis")]
public sealed class TatamisController : ControllerBase
{
    private readonly ITatamisStore _tatamisStore;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>
    /// Initializes a new controller instance.
    /// </summary>
    public TatamisController(ITatamisStore tatamisStore, ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(tatamisStore);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _tatamisStore = tatamisStore;
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Returns all tatamis for a tournament, ordered by display sequence.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Tatami>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Tatami>>> GetAllAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var tatamis = await _tatamisStore.GetAllAsync(tournamentId, cancellationToken);
        return Ok(tatamis);
    }

    /// <summary>
    /// Returns one tatami by identifier.
    /// </summary>
    [Authorize]
    [HttpGet("{tatamisId:guid}")]
    [ProducesResponseType(typeof(Tatami), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Tatami>> GetByIdAsync(
        Guid tournamentId,
        Guid tatamisId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var tatami = await _tatamisStore.GetByIdAsync(tatamisId, cancellationToken);
        if (tatami is null || tatami.TournamentId != tournamentId)
        {
            return NotFound();
        }

        return Ok(tatami);
    }

    /// <summary>
    /// Creates a tatami within a tournament.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost]
    [ProducesResponseType(typeof(Tatami), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Tatami>> CreateAsync(
        Guid tournamentId,
        [FromBody] CreateTatamiRequest request,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var created = await _tatamisStore.CreateAsync(
            tournamentId,
            request.Name,
            request.DisplayOrder,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetByIdAsync),
            new { tournamentId, tatamisId = created.Id },
            created);
    }

    /// <summary>
    /// Updates a tatami.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPut("{tatamisId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(
        Guid tournamentId,
        Guid tatamisId,
        [FromBody] UpdateTatamiRequest request,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var tatami = await _tatamisStore.GetByIdAsync(tatamisId, cancellationToken);
        if (tatami is null || tatami.TournamentId != tournamentId)
        {
            return NotFound();
        }

        await _tatamisStore.UpdateAsync(
            tatamisId,
            request.Name,
            request.DisplayOrder,
            request.IsActive,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Deletes a tatami.
    /// </summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpDelete("{tatamisId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        Guid tournamentId,
        Guid tatamisId,
        CancellationToken cancellationToken)
    {
        if (!await TournamentExistsAsync(tournamentId, cancellationToken))
        {
            return NotFound();
        }

        var tatami = await _tatamisStore.GetByIdAsync(tatamisId, cancellationToken);
        if (tatami is null || tatami.TournamentId != tournamentId)
        {
            return NotFound();
        }

        await _tatamisStore.DeleteAsync(tatamisId, cancellationToken);
        return NoContent();
    }

    private async Task<bool> TournamentExistsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        return tournament is not null;
    }
}
