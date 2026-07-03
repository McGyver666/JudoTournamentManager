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

    /// <summary>
    /// Creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Last update timestamp in UTC.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
