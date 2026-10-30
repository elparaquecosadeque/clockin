using ClockIn.Application.Common.Exceptions;
using ClockIn.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClockIn.Api.Common;

/// <summary>Turns exceptions into RFC 9457 ProblemDetails (CODE_STYLE.md, Error handling).</summary>
public sealed partial class ExceptionHandler(IProblemDetailsService problemDetails, ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            ValidationException e => new ValidationProblemDetails(e.Errors
                .GroupBy(f => ToCamelCase(f.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more fields are invalid.",
            },
            UnauthorizedException e => Problem(StatusCodes.Status401Unauthorized, "Unauthorized.", e.Message),
            NotFoundException e => Problem(StatusCodes.Status404NotFound, "Not found.", e.Message),
            ForbiddenException e => Problem(StatusCodes.Status403Forbidden, "Forbidden.", e.Message),
            ConflictException e => Problem(StatusCodes.Status409Conflict, "Conflict.", e.Message, e.Code),
            DomainException e => Problem(StatusCodes.Status422UnprocessableEntity, "A business rule was broken.", e.Message, e.Code),
            _ => null,
        };

        if (problem is null)
        {
            LogUnhandled(logger, exception);
            problem = Problem(StatusCodes.Status500InternalServerError, "An unexpected error occurred.", detail: null);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string? detail, string? code = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return problem;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
