using System.Text.Json.Serialization;

namespace ShiaiManager.Api.Data;

/// <summary>
/// Persistence model for one team entered on a team matchday.
/// </summary>
public sealed class TeamMatchdayTeamRecord
{
    /// <summary>Unique team identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning team-matchday tournament.</summary>
    public Guid TournamentId { get; set; }

    /// <summary>Existing club represented by this team.</summary>
    public Guid ClubId { get; set; }

    /// <summary>Team display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Owning tournament navigation.</summary>
    [JsonIgnore]
    public TournamentRecord? Tournament { get; set; }

    /// <summary>Represented club navigation.</summary>
    [JsonIgnore]
    public ClubRecord? Club { get; set; }
}