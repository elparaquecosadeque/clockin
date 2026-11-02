using ClockIn.Application.Timesheets.Commands;
using ClockIn.Application.Timesheets.Queries;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Hosting.Auth;
using ClockIn.PublicApi.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClockIn.PublicApi.Controllers;

[ApiController]
[Route("api/v1/timesheets/mine")]
[Authorize(Policy = PoliciesConstants.PunchTime)]
public sealed class TimesheetsController : ControllerBase
{
    /// <summary>Read my week, including entry IDs, history and the version needed for editing.</summary>
    [HttpGet("{weekStart}")]
    public async Task<TimesheetDto> GetAsync(
        DateOnly weekStart,
        [FromServices] GetTimesheetHandler handler,
        [FromServices] GetTimesheetValidator validator,
        CancellationToken ct)
    {
        var query = new GetTimesheetQuery(null, weekStart);
        await validator.ValidateAndThrowAsync(query, ct);
        return await handler.HandleAsync(query, ct);
    }

    /// <summary>Submit my ended Draft week. All entries must be closed; empty weeks are allowed.</summary>
    [HttpPost("{weekStart}/submit")]
    [ServiceFilter(typeof(TransactionFilter))]
    public async Task<NoContentResult> SubmitAsync(
        DateOnly weekStart,
        [FromServices] SubmitTimesheetHandler handler,
        [FromServices] SubmitTimesheetValidator validator,
        CancellationToken ct)
    {
        var command = new SubmitTimesheetCommand(null, weekStart);
        await validator.ValidateAndThrowAsync(command, ct);
        await handler.HandleAsync(command, ct);
        return NoContent();
    }
}
