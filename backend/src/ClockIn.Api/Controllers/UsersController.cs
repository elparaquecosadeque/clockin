using ClockIn.Api.Auth;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace ClockIn.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.ManageUsers)]
public sealed class UsersController(IMessageBus bus) : ControllerBase
{
    [HttpPost("{id:guid}/access-links")]
    public Task<AccessLinkDto> GenerateAccessLink(Guid id, CancellationToken ct) =>
        bus.InvokeAsync<AccessLinkDto>(new GenerateAccessLinkCommand(id), ct);
}
