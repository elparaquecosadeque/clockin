using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;

namespace ClockIn.Application.Teams.Commands;

/// <summary>
/// Admins only (permission policy). Open entries and pending work need no change: the approver is derived
/// from the team at decision time.
/// </summary>
public sealed record MoveEmployeeCommand(Guid UserId, Guid TeamId);

public sealed class MoveEmployeeHandler(IUserRepository users, ITeamRepository teams)
{
    public async Task HandleAsync(MoveEmployeeCommand command, CancellationToken ct)
    {
        User user = await users.FindAsync(new UserId(command.UserId), ct) ?? throw new NotFoundException("User not found.");
        Team team = await teams.FindAsync(new TeamId(command.TeamId), ct) ?? throw new NotFoundException("Team not found.");
        user.MoveTo(team);
        await users.SaveAsync(user, ct);
    }
}
