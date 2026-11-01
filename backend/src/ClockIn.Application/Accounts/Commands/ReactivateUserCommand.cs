using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Admins only (permission policy). An employee goes back to the chosen team, or else to the one they had.</summary>
public sealed record ReactivateUserCommand(Guid UserId, Guid? TeamId) : ITransactional;

public sealed class ReactivateUserHandler(
    ICurrentUser currentUser,
    IUserRepository usersRepository,
    ITeamRepository teamsRepository,
    IAuditLog audit,
    TimeProvider time)
{
    public async Task<AccessLinkDto> HandleAsync(ReactivateUserCommand command, CancellationToken ct)
    {
        User user = await usersRepository.FindAsync(new UserId(command.UserId), ct) ?? throw new NotFoundException("User not found.");
        Team? team = (command.TeamId is { } chosen ? new TeamId(chosen) : user.TeamId) is { } teamId
            ? await teamsRepository.FindAsync(teamId, ct) ?? throw new NotFoundException("Team not found.")
            : null;
        AccessLink link = user.Reactivate(team, time.GetUtcNow());
        await usersRepository.SaveOrUpdateAsync(user, ct);
        if (team is not null)
        {
            // Saving the team too checks its version: a concurrent archive or change of it conflicts. Users before teams,
            // like every handler, so two of them can't deadlock.
            await teamsRepository.SaveOrUpdateAsync(team, ct);
        }

        audit.AccessLinkGenerated(currentUser.Id, user.Id);
        return new AccessLinkDto(link.Token, link.ExpiresAt);
    }
}
