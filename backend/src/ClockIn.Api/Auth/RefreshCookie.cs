using ClockIn.Application.Accounts.Results;

namespace ClockIn.Api.Auth;

/// <summary>The refresh token travels only in this cookie; JavaScript never reads it (CODE_STYLE.md, HTTP, session and errors).</summary>
public static class RefreshCookie
{
    public const string Name = "refresh_token";

    // Scoped to the auth endpoints, so no other request carries it.
    private const string Path = "/api/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Append(HttpResponse response, SessionResult session) =>
        response.Cookies.Append(Name, session.RefreshToken, Options(session.RefreshTokenExpiresAt));

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, Options(expires: null));

    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
    };
}
