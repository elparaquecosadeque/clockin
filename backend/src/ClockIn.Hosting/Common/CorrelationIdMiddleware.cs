using System.Text.RegularExpressions;

namespace ClockIn.Hosting.Common;

/// <summary>
/// Takes <c>X-Correlation-Id</c> from the request, or generates one, and adds it to the
/// response, every log entry of the request and every ProblemDetails.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-Id";
    private const string ItemKey = "CorrelationId";

    public static string? Get(HttpContext context) => context.Items[ItemKey] as string;

    public async Task InvokeAsync(HttpContext context)
    {
        string incoming = context.Request.Headers[Header].ToString();
        // The header comes from outside: accept only a short, plain token.
        string correlationId = SafeId().IsMatch(incoming) ? incoming : Guid.CreateVersion7().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await next(context);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex SafeId();
}
