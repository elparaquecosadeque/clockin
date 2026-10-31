using ClockIn.Application.Timesheets.Commands;
using ClockIn.Application.Timesheets.Queries;
using ClockIn.Application.Timesheets.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace ClockIn.Api.Controllers;

/// <summary>Weeks and their workflow. Access depends on owner, team and role, so the handlers check it.</summary>
[ApiController]
[Route("api/timesheets")]
[Authorize]
public sealed class TimesheetsController(IMessageBus bus) : ControllerBase
{
    public sealed record CommentRequest(string Comment);

    public sealed record ReasonRequest(string Reason);

    public sealed record CreatedRequest(Guid Id);

    [HttpGet("mine/{weekStart}")]
    public Task<TimesheetDto> GetMineAsync(DateOnly weekStart, CancellationToken ct) =>
        bus.InvokeAsync<TimesheetDto>(new GetTimesheetQuery(null, weekStart), ct);

    [HttpGet("users/{ownerId:guid}/{weekStart}")]
    public Task<TimesheetDto> GetAsync(Guid ownerId, DateOnly weekStart, CancellationToken ct) =>
        bus.InvokeAsync<TimesheetDto>(new GetTimesheetQuery(ownerId, weekStart), ct);

    [HttpPost("mine/{weekStart}/submit")]
    public Task<NoContentResult> SubmitMineAsync(DateOnly weekStart, CancellationToken ct) =>
        NoContentAfterAsync(new SubmitTimesheetCommand(null, weekStart), ct);

    [HttpPost("users/{ownerId:guid}/{weekStart}/submit")]
    public Task<NoContentResult> SubmitAsync(Guid ownerId, DateOnly weekStart, CancellationToken ct) =>
        NoContentAfterAsync(new SubmitTimesheetCommand(ownerId, weekStart), ct);

    [HttpPost("{id:guid}/approve")]
    public Task<NoContentResult> ApproveAsync(Guid id, CancellationToken ct) =>
        NoContentAfterAsync(new ApproveTimesheetCommand(id), ct);

    [HttpPost("{id:guid}/return")]
    public Task<NoContentResult> ReturnAsync(Guid id, CommentRequest request, CancellationToken ct) =>
        NoContentAfterAsync(new ReturnTimesheetCommand(id, request.Comment), ct);

    [HttpPost("{id:guid}/reopen")]
    public Task<NoContentResult> ReopenAsync(Guid id, ReasonRequest request, CancellationToken ct) =>
        NoContentAfterAsync(new ReopenTimesheetCommand(id, request.Reason), ct);

    [HttpPost("{id:guid}/correction-requests")]
    public async Task<CreatedRequest> RequestCorrectionAsync(Guid id, ReasonRequest request, CancellationToken ct) =>
        new(await bus.InvokeAsync<Guid>(new RequestCorrectionCommand(id, request.Reason), ct));

    private async Task<NoContentResult> NoContentAfterAsync(object command, CancellationToken ct)
    {
        await bus.InvokeAsync(command, ct);
        return NoContent();
    }
}
