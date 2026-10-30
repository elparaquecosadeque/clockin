using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Mappings;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Queries;

public sealed record GetMeQuery;

public sealed class GetMeHandler(ICurrentUser currentUser, IUserRepository users)
{
    // ponytail: reads through the repository; switch to an IUserReads projection when this query grows.
    public async Task<MeDto> HandleAsync(GetMeQuery query, CancellationToken ct)
    {
        User user = await users.FindAsync(currentUser.Id, ct) ?? throw new UnauthorizedException();
        return user.ToMeDto();
    }
}
