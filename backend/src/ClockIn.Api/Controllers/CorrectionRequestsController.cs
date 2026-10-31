using ClockIn.Application.Timesheets.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace ClockIn.Api.Controllers;

/// <summary>The approver's decisions. Who the approver is is derived by the handlers.</summary>
[ApiController]
[Route("api/correction-requests")]
[Authorize]
public sealed class CorrectionRequestsController(IMessageBus bus) : ControllerBase
{
    public sealed record AcceptRequest(string? Comment);

    public sealed record DeclineRequest(string Comment);

    [HttpPost("{id:guid}/accept")]
    public async Task<NoContentResult> AcceptAsync(Guid id, AcceptRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new AcceptCorrectionCommand(id, request.Comment), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/decline")]
    public async Task<NoContentResult> DeclineAsync(Guid id, DeclineRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeclineCorrectionCommand(id, request.Comment), ct);
        return NoContent();
    }
}
