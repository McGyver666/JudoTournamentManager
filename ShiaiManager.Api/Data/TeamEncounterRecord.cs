namespace ShiaiManager.Api.Data;

/// <summary>
/// Persistence model for a configured team encounter on one matchday.
/// </summary>
public sealed class TeamEncounterRecord
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public Guid? TatamiId { get; set; }
    public int DisplayOrder { get; set; }
    public Guid? NoShowTeamId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}