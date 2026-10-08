using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShiaiManager.Api.Tests;

/// <summary>
/// Guards that the frontend emits no inline scripts, which the API CSP (<c>script-src 'self'</c>) would block.
/// </summary>
[Trait("Category", "UnitTest")]
public sealed partial class FrontendCspCompatibilityTests
{
    [Fact]
    public void IndexHtml_ContainsNoInlineScripts()
    {
        var html = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "frontend", "src", "index.html"));

        var inlineScripts = ScriptTagRegex().Matches(html).Where(m => !m.Groups["attrs"].Value.Contains("src="));

        Assert.Empty(inlineScripts);
    }

    [Fact]
    public void ProductionBuild_DisablesCriticalCssInlining()
    {
        // Critical CSS inlining injects an inline loader script that the CSP blocks, leaving styles.css unapplied.
        using var angularJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), "frontend", "angular.json")));

        var inlineCritical = angularJson.RootElement
            .GetProperty("projects").GetProperty("shiai-frontend").GetProperty("architect")
            .GetProperty("build").GetProperty("configurations").GetProperty("production")
            .GetProperty("optimization").GetProperty("styles").GetProperty("inlineCritical");

        Assert.False(inlineCritical.GetBoolean());
    }

    [GeneratedRegex(@"<script(?<attrs>[^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagRegex();

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ShiaiManager.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
