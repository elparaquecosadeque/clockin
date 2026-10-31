using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;
using FluentValidation;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>
/// Admins only (permission policy). Pending timesheets need no change: the approver is derived from the team
/// at decision time, so a promoted employee's lands in the admin queue.
/// </summary>
public sealed record ChangeRoleCommand(Guid UserId, string Role, Guid? TeamId);

public sealed class ChangeRoleValidator : AbstractValidator<ChangeRoleCommand>
{
    public ChangeRoleValidator()
    {
        RuleFor(c => c.Role).MustBeRole();
        RuleFor(c => c.TeamId).NotEmpty().When(c => c.Role == nameof(Role.Employee)).WithMessage("Choose a team.");
        RuleFor(c => c.TeamId).Empty().When(c => c.Role != nameof(Role.Employee)).WithMessage("Only employees belong to a team.");
    }
}

public sealed class ChangeRoleHandler(ICurrentUser currentUser, IUserRepository usersRepository, ITeamRepository teamsRepository, IAuditLog audit)
{
    public async Task HandleAsync(ChangeRoleCommand command, CancellationToken ct)
    {
        User user = await usersRepository.FindAsync(new UserId(command.UserId), ct) ?? throw new NotFoundException("User not found.");
        var role = Enum.Parse<Role>(command.Role);
        Team? team = command.TeamId is { } teamId
            ? await teamsRepository.FindAsync(new TeamId(teamId), ct) ?? throw new NotFoundException("Team not found.")
            : null;
        if (user.Role == Role.Supervisor && role == Role.Employee && await teamsRepository.LeadsActiveTeamAsync(user.Id, ct))
        {
            throw new DomainException("TeamNeedsNewSupervisor", "This supervisor leads a team. Give the team a new supervisor first.");
        }

        user.ChangeRole(role, team);
        await usersRepository.SaveOrUpdateAsync(user, ct);
        audit.RoleChanged(currentUser.Id, user.Id, role);
    }
}
