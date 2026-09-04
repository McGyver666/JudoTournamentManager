namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Deployed application version, resolved from the build-time assembly metadata.
/// </summary>
/// <param name="Version">Release version string, e.g. "1.2.3" or "1.2.3+build.57".</param>
public sealed record VersionResponse(string Version);
