using System.ComponentModel.DataAnnotations;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for editing the scores and winner of an already completed fight (Kampfübersicht).
/// Only available to Admin users.
/// </summary>
public sealed record EditFightResultRequest
{
    /// <summary>Ippon count for the White athlete (≥ 0).</summary>
    [Range(0, 99)] public int WhiteIpponCount { get; init; }

    /// <summary>Waza-ari count for the White athlete (≥ 0).</summary>
    [Range(0, 99)] public int WhiteWazaAriCount { get; init; }

    /// <summary>Yuko count for the White athlete (≥ 0).</summary>
    [Range(0, 99)] public int WhiteYukoCount { get; init; }

    /// <summary>Shido (penalty) count for the White athlete (≥ 0).</summary>
    [Range(0, 99)] public int WhitePenalties { get; init; }

    /// <summary>Ippon count for the Blue athlete (≥ 0).</summary>
    [Range(0, 99)] public int BlueIpponCount { get; init; }

    /// <summary>Waza-ari count for the Blue athlete (≥ 0).</summary>
    [Range(0, 99)] public int BlueWazaAriCount { get; init; }

    /// <summary>Yuko count for the Blue athlete (≥ 0).</summary>
    [Range(0, 99)] public int BlueYukoCount { get; init; }

    /// <summary>Shido (penalty) count for the Blue athlete (≥ 0).</summary>
    [Range(0, 99)] public int BluePenalties { get; init; }

    /// <summary>ID of the fight's winner (must be one of the fight's participants).</summary>
    public Guid WinnerId { get; init; }

    /// <summary>
    /// When true, applies the edit even if already-started downstream fights are affected
    /// (those fights will be reset to Pending). When false, returns ConfirmationRequired instead.
    /// </summary>
    public bool Confirmed { get; init; }
}
