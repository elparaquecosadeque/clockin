using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Admins only (permission policy). An employee goes back to the chosen team, or else to the one they had.</summary>
public sealed record ReactivateUserCommand(Guid UserId, Guid? TeamId);

public sealed class ReactivateUserHandler(
    ICurrentUser currentUser, IUserRepository users, ITeamRepository teams, IAuditLog audit, TimeProvider time)
{
    public async Task<AccessLinkDto> HandleAsync(ReactivateUserCommand command, CancellationToken ct)
    {
        User user = await users.FindAsync(new UserId(command.UserId), ct) ?? throw new NotFoundException("User not found.");
        Team? team = (command.TeamId is { } chosen ? new TeamId(chosen) : user.TeamId) is { } teamId
            ? await teams.FindAsync(teamId, ct) ?? throw new NotFoundException("Team not found.")
            : null;
        AccessLink link = user.Reactivate(team, time.GetUtcNow());
        await users.SaveAsync(user, ct);
        audit.AccessLinkGenerated(currentUser.Id, user.Id);
        return new AccessLinkDto(link.Token, link.ExpiresAt);
    }
}
