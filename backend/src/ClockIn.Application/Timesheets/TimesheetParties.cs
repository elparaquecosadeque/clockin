using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Teams.Interfaces;
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

    public static async Task<TimesheetParties> LoadAsync(
        IUserRepository usersRepository, ITeamRepository teamsRepository, UserId actorId, UserId ownerId, CancellationToken ct)
    {
        var actor = await usersRepository.FindAsync(actorId, ct) ?? throw new UnauthorizedException();
        var owner = actorId == ownerId ? actor : await usersRepository.FindAsync(ownerId, ct) ?? throw new NotFoundException("User not found.");
        Team? team = owner.TeamId is { } teamId ? await teamsRepository.FindAsync(teamId, ct) : null;
        return new TimesheetParties(actor, owner, team);
    }

    public static void Require(bool allowed)
    {
        if (!allowed)
        {
            throw new ForbiddenException("You can't do this on this timesheet.");
        }
    }
}
