using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Teams.Mappings;
using ClockIn.Application.Teams.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;
using FluentValidation;

namespace ClockIn.Application.Teams.Commands;

/// <summary>Admins only (permission policy).</summary>
public sealed record CreateTeamCommand(string Name, Guid? SupervisorId);

public sealed class CreateTeamValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamValidator()
    {
        RuleFor(c => c.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Enter a name.")
            .Must(n => n.Trim().Length <= Team.MaxNameLength).WithMessage($"Use at most {Team.MaxNameLength} characters.");
        RuleFor(c => c.SupervisorId).NotEmpty();
    }
}

public sealed class CreateTeamHandler(IUserRepository usersRepository, ITeamRepository teamsRepository)
{
    public async Task<TeamDto> HandleAsync(CreateTeamCommand command, CancellationToken ct)
    {
        User supervisor = await usersRepository.FindAsync(new UserId(command.SupervisorId!.Value), ct)
            ?? throw new NotFoundException("Supervisor not found.");
        if (await teamsRepository.NameInUseAsync(command.Name, ct))
        {
            throw new ConflictException("TeamNameInUse", "Another team, maybe archived, already has this name.");
        }

        var team = Team.Create(command.Name, supervisor);
        if (await teamsRepository.LeadsActiveTeamAsync(supervisor.Id, ct))
        {
            throw new DomainException("SupervisorAlreadyLeadsTeam", "This supervisor already leads a team.");
        }

        await teamsRepository.SaveOrUpdateAsync(team, ct);
        return team.ToDto();
    }
}
