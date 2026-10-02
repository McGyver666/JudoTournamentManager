using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using ShiaiManager.Api.Controllers;

namespace ShiaiManager.Api.Tests;

/// <summary>
/// Guards against drift between the API restore upload limit and the nginx
/// reverse-proxy limits shipped in <c>deploy/</c> (issue #63).
/// </summary>
[Trait("Category", "UnitTest")]
public sealed partial class NginxUploadLimitTests
{
    private const long GeneralLimitBytes = 20L * 1024 * 1024;

    public static TheoryData<string> NginxConfigFiles => new()
    {
        Path.Combine("deploy", "shiai-manager.nginx.conf"),
        Path.Combine("deploy", "install_release.sh"),
    };

    [Theory]
    [MemberData(nameof(NginxConfigFiles))]
    public void RestoreLocation_AllowsSameBodySizeAsApi(string relativePath)
    {
        var config = File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

        var match = RestoreLocationRegex().Match(config);

        Assert.True(match.Success, $"{relativePath} has no 'location = /api/tournaments/restore' block.");
        Assert.Equal(GetApiRestoreLimitBytes(), ParseSize(match.Groups["size"].Value));
    }

    [Theory]
    [MemberData(nameof(NginxConfigFiles))]
    public void ServerBlock_KeepsGeneralBodySizeLimit(string relativePath)
    {
        var config = File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

        var match = ServerLevelLimitRegex().Match(config);

        Assert.True(match.Success, $"{relativePath} has no server-level client_max_body_size.");
        Assert.Equal(GeneralLimitBytes, ParseSize(match.Groups["size"].Value));
    }

    private static long GetApiRestoreLimitBytes()
    {
        var method = typeof(BackupController).GetMethod(nameof(BackupController.RestoreAsync))!;
        var attribute = method.GetCustomAttribute<RequestSizeLimitAttribute>()!;
        return ((IRequestSizeLimitMetadata)attribute).MaxRequestBodySize!.Value;
    }

    private static long ParseSize(string value)
    {
        if (char.IsDigit(value[^1]))
        {
            return long.Parse(value);
        }

        var number = long.Parse(value[..^1]);
        return char.ToLowerInvariant(value[^1]) switch
        {
            'k' => number * 1024,
            'm' => number * 1024 * 1024,
            _ => number * 1024 * 1024 * 1024,
        };
    }

    [GeneratedRegex(@"location\s*=\s*/api/tournaments/restore\s*\{[^}]*?client_max_body_size\s+(?<size>\d+[kKmMgG]?)\s*;")]
    private static partial Regex RestoreLocationRegex();

    // Matches the directive indented at server level (4 spaces), not inside a location block.
    [GeneratedRegex(@"^ {4}client_max_body_size\s+(?<size>\d+[kKmMgG]?)\s*;", RegexOptions.Multiline)]
    private static partial Regex ServerLevelLimitRegex();

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
