using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Mappings;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Queries;

public sealed record GetMeQuery;

public sealed class GetMeHandler(
    ICurrentUser currentUser,
    IUserRepository usersRepository,
    ITeamRepository teamsRepository)
{
    // ponytail: reads through the repositories; switch to an IUserReads projection when this query grows.
    public async Task<MeDto> HandleAsync(GetMeQuery query, CancellationToken ct)
    {
        User user = await usersRepository.FindAsync(currentUser.Id, ct) ?? throw new UnauthorizedException();
        if (user.TeamId is not { } teamId || await teamsRepository.FindAsync(teamId, ct) is not { } team)
        {
            return user.ToMeDto();
        }

        User? supervisor = await usersRepository.FindAsync(team.SupervisorId, ct);
        return user.ToMeDto(team.Name, supervisor?.Name);
    }
}
