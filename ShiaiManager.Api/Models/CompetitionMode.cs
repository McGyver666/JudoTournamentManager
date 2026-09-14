using System.Text.Json.Serialization;

namespace ShiaiManager.Api.Models;

/// <summary>
/// Identifies the competition workflow represented by a tournament.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompetitionMode
{
    /// <summary>Existing individual-tournament workflow.</summary>
    Individual,

    /// <summary>One local NWJV team competition matchday.</summary>
    TeamMatchday
}