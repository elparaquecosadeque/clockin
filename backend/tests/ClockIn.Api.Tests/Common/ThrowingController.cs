using ClockIn.Application.Common.Exceptions;
using ClockIn.Domain.Common;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace ClockIn.Api.Tests.Common;

/// <summary>Test-only endpoint that throws each kind of exception the error handler maps.</summary>
[ApiController]
[Route("test/throw")]
public sealed class ThrowingController : ControllerBase
{
    [HttpGet("{kind}")]
    public IActionResult Throw(string kind) => kind switch
    {
        "validation" => throw new ValidationException([new ValidationFailure("Note", "Note is required.")]),
        "not-found" => throw new NotFoundException("Entry not found."),
        "forbidden" => throw new ForbiddenException("Not your entry."),
        "conflict" => throw new ConflictException("ConcurrencyConflict", "Changed by someone else."),
        "domain" => throw new DomainException("EntryAlreadyOpen", "An entry is already open."),
        _ => throw new InvalidOperationException("secret internal detail"),
    };
}
