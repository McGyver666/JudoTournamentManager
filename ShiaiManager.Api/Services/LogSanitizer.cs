namespace ShiaiManager.Api.Services;

/// <summary>Neutralizes user-controlled values before they are written to logs.</summary>
internal static class LogSanitizer
{
    /// <summary>Removes line breaks so a value cannot forge additional log entries.</summary>
    public static string Sanitize(string? value) =>
        (value ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
}
