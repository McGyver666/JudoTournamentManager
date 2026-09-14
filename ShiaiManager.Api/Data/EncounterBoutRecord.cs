namespace ShiaiManager.Api.Data;

/// <summary>
/// Links one standard fight to a team encounter leg and weight class.
/// </summary>
public sealed class EncounterBoutRecord
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public int LegNumber { get; set; }
    public int WeightClassIndex { get; set; }
    public Guid FightId { get; set; }
}