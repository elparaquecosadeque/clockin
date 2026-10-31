using ClockIn.Api.Auth;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Queries;
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
    public sealed record RoleRequest(string Role, Guid? TeamId);

    public sealed record TeamRequest(Guid? TeamId);

    [HttpGet]
    public Task<IReadOnlyList<UserListItemDto>> ListAsync(CancellationToken ct) =>
        bus.InvokeAsync<IReadOnlyList<UserListItemDto>>(new ListUsersQuery(), ct);

    [HttpPost]
    public Task<AccessLinkDto> CreateAsync(CreateUserCommand command, CancellationToken ct) =>
        bus.InvokeAsync<AccessLinkDto>(command, ct);

    [HttpPost("{id:guid}/access-links")]
    public Task<AccessLinkDto> GenerateAccessLink(Guid id, CancellationToken ct) =>
        bus.InvokeAsync<AccessLinkDto>(new GenerateAccessLinkCommand(id), ct);

    [HttpPut("{id:guid}/role")]
    public Task<NoContentResult> ChangeRoleAsync(Guid id, RoleRequest request, CancellationToken ct) =>
        NoContentAfterAsync(new ChangeRoleCommand(id, request.Role, request.TeamId), ct);

    [HttpPost("{id:guid}/deactivate")]
    public Task<NoContentResult> DeactivateAsync(Guid id, CancellationToken ct) =>
        NoContentAfterAsync(new DeactivateUserCommand(id), ct);

    [HttpPost("{id:guid}/reactivate")]
    public Task<AccessLinkDto> ReactivateAsync(Guid id, TeamRequest request, CancellationToken ct) =>
        bus.InvokeAsync<AccessLinkDto>(new ReactivateUserCommand(id, request.TeamId), ct);

    [HttpPost("{id:guid}/unlock")]
    public Task<NoContentResult> UnlockAsync(Guid id, CancellationToken ct) =>
        NoContentAfterAsync(new UnlockUserCommand(id), ct);

    private async Task<NoContentResult> NoContentAfterAsync(object command, CancellationToken ct)
    {
        await bus.InvokeAsync(command, ct);
        return NoContent();
    }
}
