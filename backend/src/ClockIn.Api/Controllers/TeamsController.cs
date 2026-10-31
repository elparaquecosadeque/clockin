using ClockIn.Api.Auth;
using ClockIn.Application.Teams.Commands;
using ClockIn.Application.Teams.Queries;
using ClockIn.Application.Teams.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace ClockIn.Api.Controllers;

[ApiController]
[Route("api/teams")]
[Authorize(Policy = Policies.ManageTeams)]
public sealed class TeamsController(IMessageBus bus) : ControllerBase
{
    public sealed record NameRequest(string Name);

    public sealed record SupervisorRequest(Guid SupervisorId);

    [HttpGet]
    public Task<IReadOnlyList<TeamListItemDto>> ListAsync(CancellationToken ct) =>
        bus.InvokeAsync<IReadOnlyList<TeamListItemDto>>(new ListTeamsQuery(), ct);

    [HttpPost]
    public Task<TeamDto> CreateAsync(CreateTeamCommand command, CancellationToken ct) => bus.InvokeAsync<TeamDto>(command, ct);

    [HttpPut("{id:guid}/name")]
    public Task<NoContentResult> RenameAsync(Guid id, NameRequest request, CancellationToken ct) =>
        NoContentAfterAsync(new RenameTeamCommand(id, request.Name), ct);

    [HttpPut("{id:guid}/supervisor")]
    public Task<NoContentResult> ChangeSupervisorAsync(Guid id, SupervisorRequest request, CancellationToken ct) =>
        NoContentAfterAsync(new ChangeTeamSupervisorCommand(id, request.SupervisorId), ct);

    [HttpPost("{id:guid}/archive")]
    public Task<NoContentResult> ArchiveAsync(Guid id, CancellationToken ct) => NoContentAfterAsync(new ArchiveTeamCommand(id), ct);

    [HttpPut("{id:guid}/employees/{userId:guid}")]
    public Task<NoContentResult> MoveEmployeeAsync(Guid id, Guid userId, CancellationToken ct) =>
        NoContentAfterAsync(new MoveEmployeeCommand(userId, id), ct);

    private async Task<NoContentResult> NoContentAfterAsync(object command, CancellationToken ct)
    {
        await bus.InvokeAsync(command, ct);
        return NoContent();
    }
}
