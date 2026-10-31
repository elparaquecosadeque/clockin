using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;
using FluentValidation;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Admins only (permission policy). Returns the activation link for the admin to hand over.</summary>
public sealed record CreateUserCommand(
    string Email, string Name, string Role, Guid? TeamId, string Language = Preferences.English, string TimeFormat = Preferences.TwentyFourHour);

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.Email).Must(e => Domain.Accounts.Email.TryCreate(e, out _)).WithMessage("Enter a valid email.");
        RuleFor(c => c.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Enter a name.")
            .Must(n => (n ?? "").Trim().Length <= User.MaxNameLength).WithMessage($"Use at most {User.MaxNameLength} characters.");
        RuleFor(c => c.Role).MustBeRole();
        RuleFor(c => c.Language).MustBeLanguage();
        RuleFor(c => c.TimeFormat).MustBeTimeFormat();
        RuleFor(c => c.TeamId).NotEmpty().When(c => c.Role == nameof(Role.Employee)).WithMessage("Choose a team.");
        RuleFor(c => c.TeamId).Empty().When(c => c.Role != nameof(Role.Employee)).WithMessage("Only employees belong to a team.");
    }
}

public sealed class CreateUserHandler(
    ICurrentUser currentUser, IUserRepository usersRepository, ITeamRepository teamsRepository, IAuditLog audit, TimeProvider time)
{
    public async Task<AccessLinkDto> HandleAsync(CreateUserCommand command, CancellationToken ct)
    {
        var user = User.Create(new Email(command.Email), command.Name, Enum.Parse<Role>(command.Role), command.Language, command.TimeFormat);
        if (command.TeamId is { } teamId)
        {
            Team team = await teamsRepository.FindAsync(new TeamId(teamId), ct) ?? throw new NotFoundException("Team not found.");
            if (team.IsArchived)
            {
                throw new DomainException("TeamIsArchived", "The team is archived. Choose an active team.");
            }

            user.MoveTo(team);
        }

        AccessLink link = user.IssueAccessLink(time.GetUtcNow());
        await usersRepository.SaveOrUpdateAsync(user, ct);
        audit.AccessLinkGenerated(currentUser.Id, user.Id);
        return new AccessLinkDto(link.Token, link.ExpiresAt);
    }
}
