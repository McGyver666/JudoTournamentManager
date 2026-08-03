namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Request payload for enabling or rotating a tournament's anonymous guest share.
/// </summary>
public sealed record GuestShareRequest
{
    /// <summary>
    /// Optional auto-off timestamp (UTC). When null the share stays active until
    /// it is switched off manually.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
