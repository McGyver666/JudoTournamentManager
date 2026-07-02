using System.Text.Json.Serialization;

namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Persistence model for a club (Verein) participating in a tournament.
/// </summary>
public sealed class ClubRecord
{
    /// <summary>Unique club identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tournament.</summary>
    public Guid TournamentId { get; set; }

    /// <summary>Club display name; unique within a tournament.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Navigation property to the owning tournament.</summary>
    [JsonIgnore]
    public TournamentRecord? Tournament { get; set; }
}
