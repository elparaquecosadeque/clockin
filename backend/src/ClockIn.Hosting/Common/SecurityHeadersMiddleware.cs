namespace ClockIn.Hosting.Common;

/// <summary>
/// Security headers on every API response (CODE_STYLE.md, Security hardening). The API only serves JSON, so nothing
/// may load from it, frame it or sniff it as another type.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        // On starting, so the headers survive the exception handler clearing the response.
        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            headers["Referrer-Policy"] = "no-referrer";
            return Task.CompletedTask;
        });
        return next(context);
    }
}
