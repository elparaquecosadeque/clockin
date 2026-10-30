using ClockIn.Api.Auth;
using ClockIn.Application.Timesheets.Commands;
using ClockIn.Application.Timesheets.Queries;
using ClockIn.Application.Timesheets.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace ClockIn.Api.Controllers;

[ApiController]
[Route("api/time-entries")]
[Authorize(Policy = Policies.PunchTime)]
public sealed class TimeEntriesController(IMessageBus bus) : ControllerBase
{
    [HttpGet("clock")]
    public Task<ClockStatusDto> GetClockStatus(CancellationToken ct) =>
        bus.InvokeAsync<ClockStatusDto>(new GetClockStatusQuery(), ct);

    [HttpPost("clock-in")]
    public Task<TimeEntryDto> ClockIn(CancellationToken ct) =>
        bus.InvokeAsync<TimeEntryDto>(new ClockInCommand(ClientIp, UserAgent), ct);

    [HttpPost("clock-out")]
    public Task<TimeEntryDto> ClockOut(CancellationToken ct) =>
        bus.InvokeAsync<TimeEntryDto>(new ClockOutCommand(ClientIp, UserAgent), ct);

    // ponytail: the direct peer's address; behind a reverse proxy, enable the forwarded-headers middleware for the real client IP.
    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string UserAgent => Request.Headers.UserAgent.ToString();
}
