namespace ShiaiManager.Api.Data;

/// <summary>
/// Persistence model for one team encounter leg and weight class.
/// </summary>
public sealed class TeamLineupEntryRecord
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public int LegNumber { get; set; }
    public Guid TeamId { get; set; }
    public int WeightClassIndex { get; set; }
    public Guid? AthleteId { get; set; }
}