namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Data-minimized public projection of a club for the anonymous match-list view.
/// Deliberately excludes contact data (name, email, phone).
/// </summary>
/// <param name="Id">Unique club identifier.</param>
/// <param name="Name">Club display name.</param>
public sealed record PublicClub(Guid Id, string Name);
