namespace ShiaiManager.Api.Services;

/// <summary>
/// Represents a generated result CSV file and its download name.
/// </summary>
public sealed record ResultsCsvExport(byte[] Content, string FileName);