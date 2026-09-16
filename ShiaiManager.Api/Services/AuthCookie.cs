using Microsoft.AspNetCore.Http;

namespace ShiaiManager.Api.Services;

internal static class AuthCookie
{
    public const string Name = "shiai_auth";

    public static CookieOptions CreateOptions(DateTimeOffset? expires = null, bool isHttps = false)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = isHttps,
            Path = "/",
            Expires = expires
        };
    }
}