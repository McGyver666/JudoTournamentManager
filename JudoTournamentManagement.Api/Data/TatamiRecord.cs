using System.Text.Json.Serialization;

namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistence model for a competition area (tatami) within a tournament.
/// </summary>
public sealed class TatamiRecord
{
    /// <summary>Unique tatami identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tournament.</summary>
    public Guid TournamentId { get; set; }

    /// <summary>Display name, e.g. "Tatami 1".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Position in the display/queue sequence (0-based).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Whether this tatami is currently in use.</summary>
    public bool IsActive { get; set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Navigation property to the owning tournament.</summary>
    [JsonIgnore]
    public TournamentRecord? Tournament { get; set; }
}
