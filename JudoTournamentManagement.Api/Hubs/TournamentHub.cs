using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace JudoTournamentManagement.Api.Hubs;

/// <summary>
/// SignalR hub for real-time tournament updates.
/// Clients join a tournament group and receive fight-state change notifications.
/// </summary>
[Authorize]
public sealed class TournamentHub : Hub
{
    private readonly ITournamentStore _tournamentStore;

    /// <summary>
    /// Initializes a new hub instance.
    /// </summary>
    public TournamentHub(ITournamentStore tournamentStore)
    {
        ArgumentNullException.ThrowIfNull(tournamentStore);
        _tournamentStore = tournamentStore;
    }

    /// <summary>
    /// Joins the SignalR group for the given tournament so the client receives
    /// <c>FightUpdated</c> and <c>CategoryFightsUpdated</c> messages for that tournament.
    /// </summary>
    public async Task JoinTournamentAsync(string tournamentId)
    {
        if (string.IsNullOrWhiteSpace(tournamentId) || !Guid.TryParse(tournamentId, out var parsedTournamentId))
        {
            throw new HubException("Ungueltige Turnier-ID.");
        }

        var tournament = await _tournamentStore.GetByIdAsync(parsedTournamentId, Context.ConnectionAborted);
        if (tournament is null)
        {
            throw new HubException("Turnier nicht gefunden.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, tournamentId);
    }

    /// <summary>
    /// Leaves the SignalR group for the given tournament.
    /// </summary>
    public async Task LeaveTournamentAsync(string tournamentId)
    {
        if (string.IsNullOrWhiteSpace(tournamentId))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tournamentId);
    }
}
