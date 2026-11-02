using ClockIn.Application.Timesheets.Commands;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Hosting.Auth;
using ClockIn.PublicApi.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClockIn.PublicApi.Controllers;

[ApiController]
[Route("api/v1/time-entries")]
[Authorize(Policy = PoliciesConstants.PunchTime)]
public sealed class TimeEntriesController : ControllerBase
{
    public sealed record EditEntryRequest(DateTimeOffset Start, DateTimeOffset? End, string Note, int Version);

    /// <summary>Open my entry using server time. Only one entry may be open.</summary>
    [HttpPost("clock-in")]
    [ServiceFilter(typeof(TransactionFilter))]
    public Task<TimeEntryDto> ClockInAsync([FromServices] ClockInHandler handler, CancellationToken ct) =>
        handler.HandleAsync(new ClockInCommand(ClientIp, UserAgent), ct);

    /// <summary>Close my open entry using server time, including an entry from an earlier week.</summary>
    [HttpPost("clock-out")]
    [ServiceFilter(typeof(TransactionFilter))]
    public Task<TimeEntryDto> ClockOutAsync([FromServices] ClockOutHandler handler, CancellationToken ct) =>
        handler.HandleAsync(new ClockOutCommand(ClientIp, UserAgent), ct);

    /// <summary>Edit my Draft entry. Version is the containing timesheet's latest version; a note is required.</summary>
    [HttpPut("{entryId:guid}")]
    [ServiceFilter(typeof(TransactionFilter))]
    public async Task<NoContentResult> EditAsync(
        Guid entryId,
        EditEntryRequest request,
        [FromServices] EditEntryHandler handler,
        [FromServices] EditEntryValidator validator,
        CancellationToken ct)
    {
        var command = new EditEntryCommand(entryId, request.Start, request.End, request.Note, request.Version);
        await validator.ValidateAndThrowAsync(command, ct);
        await handler.HandleAsync(command, ct);
        return NoContent();
    }

    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string UserAgent => Request.Headers.UserAgent.ToString();
}
