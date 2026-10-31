using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Teams;
using FluentValidation;

namespace ClockIn.Application.Teams.Commands;

/// <summary>Admins only (permission policy).</summary>
public sealed record RenameTeamCommand(Guid TeamId, string Name);

public sealed class RenameTeamValidator : AbstractValidator<RenameTeamCommand>
{
    public RenameTeamValidator()
    {
        RuleFor(c => c.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Enter a name.")
            .Must(n => (n ?? "").Trim().Length <= Team.MaxNameLength).WithMessage($"Use at most {Team.MaxNameLength} characters.");
    }
}

public sealed class RenameTeamHandler(ITeamRepository teamsRepository)
{
    public async Task HandleAsync(RenameTeamCommand command, CancellationToken ct)
    {
        Team team = await teamsRepository.FindAsync(new TeamId(command.TeamId), ct) ?? throw new NotFoundException("Team not found.");
        // Changing only the case keeps the normalized name, which the team itself holds.
        if (Team.NormalizeName(command.Name) != Team.NormalizeName(team.Name) && await teamsRepository.NameInUseAsync(command.Name, ct))
        {
            throw new ConflictException("TeamNameInUse", "Another team, maybe archived, already has this name.");
        }

        team.Rename(command.Name);
        await teamsRepository.SaveOrUpdateAsync(team, ct);
    }
}
