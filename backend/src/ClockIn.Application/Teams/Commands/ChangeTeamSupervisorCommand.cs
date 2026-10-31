using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;

namespace ClockIn.Application.Teams.Commands;

/// <summary>Admins only (permission policy). Pending work follows the team; past approvals keep their approver.</summary>
public sealed record ChangeTeamSupervisorCommand(Guid TeamId, Guid SupervisorId);

public sealed class ChangeTeamSupervisorHandler(IUserRepository usersRepository, ITeamRepository teamsRepository)
{
    public async Task HandleAsync(ChangeTeamSupervisorCommand command, CancellationToken ct)
    {
        Team team = await teamsRepository.FindAsync(new TeamId(command.TeamId), ct) ?? throw new NotFoundException("Team not found.");
        User supervisor = await usersRepository.FindAsync(new UserId(command.SupervisorId), ct)
            ?? throw new NotFoundException("Supervisor not found.");
        if (await teamsRepository.LeadsActiveTeamAsync(supervisor.Id, ct))
        {
            throw new DomainException("SupervisorAlreadyLeadsTeam", "This supervisor already leads a team.");
        }

        team.ChangeSupervisor(supervisor);
        await teamsRepository.SaveOrUpdateAsync(team, ct);
    }
}
