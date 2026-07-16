namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Represents a tournament aggregate in the MVP scope.
/// </summary>
/// <param name="Id">Unique tournament identifier.</param>
/// <param name="Name">Tournament display name.</param>
/// <param name="Date">Tournament date.</param>
/// <param name="Venue">Venue name or address.</param>
/// <param name="Organizer">Organizer name.</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
/// <param name="UpdatedAtUtc">Last update timestamp in UTC.</param>
public sealed record Tournament(
    Guid Id,
    string Name,
    DateOnly Date,
    string Venue,
    string Organizer,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>
    /// Side color used for the non-white athlete in the UI.
    /// </summary>
    public string AccentSideColor { get; init; } = "Blue";

    /// <summary>Hold duration in seconds required for Ippon. Default: 20 s (DJB).</summary>
    public int OsaeKomiIpponSeconds { get; init; } = 20;

    /// <summary>Hold duration in seconds required for Waza-ari. Default: 10 s (DJB).</summary>
    public int OsaeKomiWazaAriSeconds { get; init; } = 10;

    /// <summary>Hold duration in seconds required for Yuko. Default: 5 s (DJB).</summary>
    public int OsaeKomiYukoSeconds { get; init; } = 5;

    /// <summary>Whether a Yuko is awarded for a hold between Yuko and Waza-ari thresholds.</summary>
    public bool OsaeKomiYukoEnabled { get; init; } = true;
}
