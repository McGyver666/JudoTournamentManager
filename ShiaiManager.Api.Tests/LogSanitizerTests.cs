using ShiaiManager.Api.Services;

namespace ShiaiManager.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class LogSanitizerTests
{
    [Theory]
    [InlineData("Judo Club\r\nAudit: forged entry", "Judo ClubAudit: forged entry")]
    [InlineData("line1\nline2\rline3", "line1line2line3")]
    [InlineData("U18", "U18")]
    [InlineData(null, "")]
    public void Sanitize_RemovesLineBreaks(string? input, string expected)
    {
        Assert.Equal(expected, LogSanitizer.Sanitize(input));
    }
}
