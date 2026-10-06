namespace ShiaiManager.Api.Contracts;

/// <summary>
/// Localizable warning shown in the preset tab.
/// </summary>
/// <param name="Key">Frontend translation key.</param>
/// <param name="AgeGroup">Affected age group, if the warning refers to one.</param>
/// <param name="Count">Affected-item count, if the warning counts items.</param>
public sealed record CategoryPresetWarning(string Key, string? AgeGroup, int? Count);
