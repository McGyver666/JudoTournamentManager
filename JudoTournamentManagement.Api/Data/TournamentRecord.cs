namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistence model for a tournament record.
/// </summary>
public sealed class TournamentRecord
{
    /// <summary>
    /// Unique tournament identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tournament display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tournament date.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Venue name or address.
    /// </summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>
    /// Organizer name.
    /// </summary>
    public string Organizer { get; set; } = string.Empty;

    /// <summary>
    /// Side color used for the non-white athlete in the UI.
    /// </summary>
    public string AccentSideColor { get; set; } = "Blue";

    /// <summary>Hold duration in seconds required for Ippon.</summary>
    public int OsaeKomiIpponSeconds { get; set; } = 20;

    /// <summary>Hold duration in seconds required for Waza-ari.</summary>
    public int OsaeKomiWazaAriSeconds { get; set; } = 10;

    /// <summary>Hold duration in seconds required for Yuko.</summary>
    public int OsaeKomiYukoSeconds { get; set; } = 5;

    /// <summary>Whether Yuko is awarded for a hold between Yuko and Waza-ari thresholds.</summary>
    public bool OsaeKomiYukoEnabled { get; set; } = true;

    /// <summary>Minimum required rest gap in seconds before an athlete should fight again.</summary>
    public int MinimumRestBetweenFightsSeconds { get; set; } = 180;

    /// <summary>
    /// Creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Last update timestamp in UTC.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
