using System.Security.Claims;
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
    private readonly IGuestShareService _guestShareService;

    /// <summary>
    /// Initializes a new hub instance.
    /// </summary>
    public TournamentHub(ITournamentStore tournamentStore, IGuestShareService guestShareService)
    {
        ArgumentNullException.ThrowIfNull(tournamentStore);
        ArgumentNullException.ThrowIfNull(guestShareService);
        _tournamentStore = tournamentStore;
        _guestShareService = guestShareService;
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

        await EnsureGuestScopeAsync(parsedTournamentId);

        var tournament = await _tournamentStore.GetByIdAsync(parsedTournamentId, Context.ConnectionAborted);
        if (tournament is null)
        {
            throw new HubException("Turnier nicht gefunden.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, tournamentId);
    }

    /// <summary>
    /// For an anonymous guest connection, restricts hub access to the guest's own
    /// tournament and re-checks that the share is still active (soft-disconnect:
    /// a guest cannot (re-)join a group once the share has been switched off).
    /// Operator roles are unaffected.
    /// </summary>
    private async Task EnsureGuestScopeAsync(Guid tournamentId)
    {
        var user = Context.User;
        if (user is null || !user.IsInRole(IGuestShareService.GuestRole))
        {
            return;
        }

        var scopedTournamentId = user.FindFirstValue(IGuestShareService.TournamentClaimType);
        if (!Guid.TryParse(scopedTournamentId, out var guestTournamentId) || guestTournamentId != tournamentId)
        {
            throw new HubException("Kein Zugriff auf dieses Turnier.");
        }

        var state = await _guestShareService.GetStateAsync(tournamentId, Context.ConnectionAborted);
        if (!state.IsActive)
        {
            throw new HubException("Die Gast-Freigabe ist nicht aktiv.");
        }
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
