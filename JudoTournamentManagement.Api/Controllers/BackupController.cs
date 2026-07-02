using System.Text.Json;
using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JudoTournamentManagement.Api.Controllers;

/// <summary>
/// API endpoints for full tournament backup and restore (A-03).
/// Both endpoints are restricted to Admin role only.
/// </summary>
[ApiController]
[Route("api/tournaments")]
[Authorize(Roles = "Admin")]
public sealed class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;
    private readonly IAuditLogService _auditLog;

    /// <summary>
    /// Initializes a new instance of <see cref="BackupController"/>.
    /// </summary>
    public BackupController(IBackupService backupService, IAuditLogService auditLog)
    {
        ArgumentNullException.ThrowIfNull(backupService);
        ArgumentNullException.ThrowIfNull(auditLog);
        _backupService = backupService;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Downloads a complete JSON backup of the specified tournament.
    /// The file includes all tournament data: tatamis, categories, clubs, athletes,
    /// registrations, fights, and audit logs.
    /// </summary>
    [HttpGet("{tournamentId:guid}/backup")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TournamentBackup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BackupAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var backup = await _backupService.BackupAsync(tournamentId, cancellationToken);
        if (backup is null)
        {
            return NotFound();
        }

        var user = User.Identity?.Name ?? "unbekannt";
        await _auditLog.LogAsync(
            tournamentId, user, "TournamentBackedUp", "Tournament", tournamentId,
            null, cancellationToken);

        var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var fileName = $"turnier-backup-{tournamentId:N}-{DateTime.UtcNow:yyyyMMdd}.json";
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json",
            fileName);
    }

    /// <summary>
    /// Restores a tournament from a previously exported JSON backup.
    /// Returns 409 Conflict if a tournament with the same ID already exists.
    /// Returns 400 Bad Request for invalid or unsupported backup format.
    /// </summary>
    [HttpPost("restore")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(typeof(TournamentBackup), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreAsync(
        [FromBody] TournamentBackup backup,
        CancellationToken cancellationToken)
    {
        if (backup is null)
        {
            ModelState.AddModelError(string.Empty, "Backup-Daten fehlen oder sind ungültig.");
            return ValidationProblem(ModelState);
        }

        var result = await _backupService.RestoreAsync(backup, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorCode == "TournamentAlreadyExists")
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Turnier existiert bereits.",
                    Detail = result.ErrorMessage,
                    Status = StatusCodes.Status409Conflict
                });
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Wiederherstellung fehlgeschlagen.");
            return ValidationProblem(ModelState);
        }

        var user = User.Identity?.Name ?? "unbekannt";
        await _auditLog.LogAsync(
            backup.Tournament.Id, user, "TournamentRestored", "Tournament", backup.Tournament.Id,
            $"Exported={backup.ExportedAtUtc:O}", cancellationToken);

        return CreatedAtAction(
            null,
            new { },
            backup);
    }
}
