using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoint for reading the audit log of a tournament (I-01). Requires the Admin role.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/tournaments/{tournamentId:guid}/audit-log")]
public sealed class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLog;
    private readonly ITournamentStore _tournamentStore;

    /// <summary>Initializes a new controller instance.</summary>
    public AuditLogController(IAuditLogService auditLog, ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _auditLog = auditLog;
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Returns the audit log entries for a tournament, newest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AuditLogEntry>>> GetAllAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await _tournamentStore.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null) return NotFound();

        return Ok(await _auditLog.GetAllAsync(tournamentId, cancellationToken));
    }
}
