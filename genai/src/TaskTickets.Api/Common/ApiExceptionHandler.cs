using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TaskTickets.Api.Common;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            ApiException error => error.StatusCode,
            BadHttpRequestException badRequest => badRequest.StatusCode,
            _ => 500
        };
        ProblemDetails problem;
        if (exception is ApiException { Field: not null } validation)
        {
            problem = new HttpValidationProblemDetails(new Dictionary<string, string[]>
            {
                [validation.Field] = [validation.Message]
            });
        }
        else
        {
            problem = new ProblemDetails
            {
                Title = status == 500 ? "An unexpected error occurred."
                    : exception is BadHttpRequestException ? "Invalid request." : exception.Message
            };
        }

        if (status == 500)
        {
            logger.LogError(exception, "Unhandled request failure {TraceId}", context.TraceIdentifier);
        }

        problem.Status = status;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
