using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace EHub.Api.Extensions;

public static class RefreshTokenCookieExtensions
{
    public const string RefreshTokenCookieName = "ehub_refresh_token";

    public static void SetRefreshTokenCookie(
        this HttpResponse response,
        string refreshToken,
        DateTimeOffset expiresAt,
        IHostEnvironment environment)
    {
        response.Cookies.Append(
            RefreshTokenCookieName,
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Expires = expiresAt,
                Path = "/api/auth"
            });
    }

    public static void DeleteRefreshTokenCookie(
        this HttpResponse response,
        IHostEnvironment environment)
    {
        response.Cookies.Delete(
            RefreshTokenCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth"
            });
    }
}
