using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class GuestShareLinkBuilderTests
{
    private static HttpRequest RequestFor(string scheme, string host)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = scheme;
        ctx.Request.Host = new HostString(host);
        return ctx.Request;
    }

    [Fact]
    public void BuildPublicUrl_WhenNoOverride_UsesRequestSchemeAndHost()
    {
        var builder = new GuestShareLinkBuilder(new ConfigurationBuilder().Build());
        var tid = Guid.NewGuid();

        var url = builder.BuildPublicUrl(RequestFor("http", "192.168.1.50:5080"), tid, "token123");

        Assert.Equal($"http://192.168.1.50:5080/public/match-lists?t=token123&tid={tid}", url);
    }

    [Fact]
    public void BuildPublicUrl_WhenOverrideConfigured_UsesOverrideAndTrimsSlash()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GuestShare:PublicBaseUrl"] = "https://turnier.example.org/"
            })
            .Build();
        var builder = new GuestShareLinkBuilder(config);
        var tid = Guid.NewGuid();

        var url = builder.BuildPublicUrl(RequestFor("http", "10.0.0.1"), tid, "token123");

        Assert.Equal($"https://turnier.example.org/public/match-lists?t=token123&tid={tid}", url);
    }

    [Fact]
    public void BuildPublicUrl_EscapesToken()
    {
        var builder = new GuestShareLinkBuilder(new ConfigurationBuilder().Build());
        var tid = Guid.NewGuid();

        var url = builder.BuildPublicUrl(RequestFor("https", "example.org"), tid, "a+b/c=");

        Assert.Contains("t=a%2Bb%2Fc%3D", url);
    }

    [Fact]
    public void BuildPublicUrl_WhenPublicHostOverHttp_Throws()
    {
        var builder = new GuestShareLinkBuilder(new ConfigurationBuilder().Build());

        Assert.Throws<GuestShareInsecureHostException>(
            () => builder.BuildPublicUrl(RequestFor("http", "turnier.example.org"), Guid.NewGuid(), "token123"));
    }

    [Theory]
    [InlineData("http", "localhost:5080", true)]
    [InlineData("http", "127.0.0.1", true)]
    [InlineData("http", "192.168.1.50:5080", true)]
    [InlineData("http", "10.0.0.1", true)]
    [InlineData("http", "172.16.4.2", true)]
    [InlineData("http", "kampfrichter-pc", true)]
    [InlineData("http", "wettkampf.local", true)]
    [InlineData("https", "turnier.example.org", true)]
    [InlineData("http", "turnier.example.org", false)]
    [InlineData("http", "8.8.8.8", false)]
    [InlineData("http", "172.32.0.1", false)]
    public void IsHostAllowedForSharing_AppliesTlsRuleForPublicHosts(string scheme, string host, bool expected)
    {
        var builder = new GuestShareLinkBuilder(new ConfigurationBuilder().Build());

        var allowed = builder.IsHostAllowedForSharing(RequestFor(scheme, host));

        Assert.Equal(expected, allowed);
    }
}
