using ClockIn.Application.Accounts.Results;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Mappings;

public static class SessionMappings
{
    public static SessionResult ToResult(this Session session, RefreshToken token) =>
        new(session.UserId.Value, session.Id.Value, session.SecurityStamp, token.Value, token.ExpiresAt);
}
