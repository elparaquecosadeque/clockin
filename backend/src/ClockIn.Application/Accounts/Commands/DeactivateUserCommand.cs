using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Admins only (permission policy). Keeps every record; pending work stays in its approver's queue.</summary>
public sealed record DeactivateUserCommand(Guid UserId) : ITransactional;

public sealed class DeactivateUserHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    ISessionRepository sessions,
    ITeamRepository teams,
    ITimesheetRepository timesheets,
    IAuditLog audit,
    TimeProvider time)
{
    public async Task HandleAsync(DeactivateUserCommand command, CancellationToken ct)
    {
        var userId = new UserId(command.UserId);
        if (userId == currentUser.Id)
        {
            throw new DomainException("CannotDeactivateSelf", "You cannot deactivate your own account.");
        }

        User user = await users.FindAsync(userId, ct) ?? throw new NotFoundException("User not found.");
        if (await timesheets.FindWithOpenEntryAsync(userId, ct) is not null)
        {
            throw new DomainException("OpenEntryMustBeClosed", "The user is clocked in. Close the open entry first.");
        }

        if (await teams.LeadsActiveTeamAsync(userId, ct))
        {
            throw new DomainException("TeamNeedsNewSupervisor", "This supervisor leads a team. Give the team a new supervisor first.");
        }

        user.Deactivate();
        await users.SaveAsync(user, ct);
        await sessions.RevokeAllAsync(userId, time.GetUtcNow(), ct);
        audit.UserDeactivated(currentUser.Id, userId);
    }
}
