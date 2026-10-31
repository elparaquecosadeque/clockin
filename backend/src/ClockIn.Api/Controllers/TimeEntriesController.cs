using ClockIn.Api.Auth;
using ClockIn.Application.Timesheets.Commands;
using ClockIn.Application.Timesheets.Queries;
using ClockIn.Application.Timesheets.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace ClockIn.Api.Controllers;

/// <summary>Punches, and manual changes to entries. Who may change whose entries is checked by the handlers.</summary>
[ApiController]
[Route("api/time-entries")]
[Authorize]
public sealed class TimeEntriesController(IMessageBus bus) : ControllerBase
{
    /// <summary><see cref="OwnerId"/> is for admins adding to someone else's week.</summary>
    public sealed record AddEntryRequest(Guid? OwnerId, DateTimeOffset Start, DateTimeOffset End, string Note);

    public sealed record EditEntryRequest(DateTimeOffset Start, DateTimeOffset? End, string Note, int Version);

    public sealed record DeleteEntryRequest(string Note, int Version);

    public sealed record CreatedEntry(Guid Id);

    [HttpGet("clock")]
    [Authorize(Policy = Policies.PunchTime)]
    public Task<ClockStatusDto> GetClockStatus(CancellationToken ct) =>
        bus.InvokeAsync<ClockStatusDto>(new GetClockStatusQuery(), ct);

    [HttpPost("clock-in")]
    [Authorize(Policy = Policies.PunchTime)]
    public Task<TimeEntryDto> ClockIn(CancellationToken ct) =>
        bus.InvokeAsync<TimeEntryDto>(new ClockInCommand(ClientIp, UserAgent), ct);

    [HttpPost("clock-out")]
    [Authorize(Policy = Policies.PunchTime)]
    public Task<TimeEntryDto> ClockOut(CancellationToken ct) =>
        bus.InvokeAsync<TimeEntryDto>(new ClockOutCommand(ClientIp, UserAgent), ct);

    [HttpPost]
    public async Task<CreatedEntry> AddAsync(AddEntryRequest request, CancellationToken ct) =>
        new(await bus.InvokeAsync<Guid>(
            new AddEntryCommand(request.OwnerId, request.Start, request.End, request.Note, ClientIp, UserAgent), ct));

    [HttpPut("{id:guid}")]
    public async Task<NoContentResult> EditAsync(Guid id, EditEntryRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new EditEntryCommand(id, request.Start, request.End, request.Note, request.Version), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<NoContentResult> DeleteAsync(Guid id, DeleteEntryRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteEntryCommand(id, request.Note, request.Version), ct);
        return NoContent();
    }

    // ponytail: the direct peer's address; behind a reverse proxy, enable the forwarded-headers middleware for the real client IP.
    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string UserAgent => Request.Headers.UserAgent.ToString();
}
