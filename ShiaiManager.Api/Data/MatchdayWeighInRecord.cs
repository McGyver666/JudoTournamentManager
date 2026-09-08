using System.Text.Json.Serialization;

namespace ShiaiManager.Api.Data;

/// <summary>
/// Persistence model for an athlete's confirmed actual weight on one matchday.
/// </summary>
public sealed class MatchdayWeighInRecord
{
    /// <summary>Unique weigh-in identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning team-matchday tournament.</summary>
    public Guid TournamentId { get; set; }

    /// <summary>Athlete whose weight was confirmed.</summary>
    public Guid AthleteId { get; set; }

    /// <summary>Confirmed actual body weight in kilograms.</summary>
    public decimal WeightKg { get; set; }

    /// <summary>UTC confirmation timestamp.</summary>
    public DateTimeOffset ConfirmedAtUtc { get; set; }

    /// <summary>Owning tournament navigation.</summary>
    [JsonIgnore]
    public TournamentRecord? Tournament { get; set; }

    /// <summary>Athlete navigation.</summary>
    [JsonIgnore]
    public AthleteRecord? Athlete { get; set; }
}