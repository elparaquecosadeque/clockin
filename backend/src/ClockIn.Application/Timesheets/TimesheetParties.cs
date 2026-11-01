using ClockIn.Application.Common.Exceptions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;

namespace ClockIn.Application.Timesheets;

/// <summary>
/// The signed-in user and a timesheet owner, with who may do what between them. The approver is derived now,
/// from the owner's current team, so pending work follows moves and supervisor changes.
/// </summary>
public sealed record TimesheetParties(User Actor, User Owner, Team? OwnerTeam)
{
    public bool IsOwner => Actor.Id == Owner.Id;

    /// <summary>The owner's team supervisor, or any admin; never the owner.</summary>
    public bool IsApprover =>
        !IsOwner
        && (Actor.Role.Can(Permissions.ReviewAll)
            || (Actor.Role.Can(Permissions.ReviewTeam) && OwnerTeam is not null && OwnerTeam.SupervisorId == Actor.Id));

    public bool CanView => IsOwner || IsApprover;

    /// <summary>Owners change their own time; admins change anyone's, with a reason.</summary>
    public bool CanChange =>
        Owner.Role.Can(Permissions.PunchTime)
        && ((IsOwner && Actor.Role.Can(Permissions.PunchTime)) || (!IsOwner && Actor.Role.Can(Permissions.ManageTimesheets)));

    public bool CanReopen => !IsOwner && Actor.Role.Can(Permissions.ManageTimesheets);

    public static void Require(bool allowed)
    {
        if (!allowed)
        {
            throw new ForbiddenException("You can't do this on this timesheet.");
        }
    }
}
