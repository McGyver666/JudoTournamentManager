using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Builds the Current / Next / On-deck fight queue for a tatami (F-01).
/// </summary>
public interface ITatamiQueueService
{
    /// <summary>
    /// Returns the queue snapshot for a tatami, or <c>null</c> when the tatami does not belong to the tournament.
    /// </summary>
    Task<TatamiQueue?> GetQueueAsync(Guid tournamentId, Guid tatamiId, CancellationToken cancellationToken);
}
