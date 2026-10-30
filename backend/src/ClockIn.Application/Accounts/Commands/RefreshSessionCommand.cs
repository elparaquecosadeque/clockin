using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Mappings;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Commands;

public sealed record RefreshSessionCommand(string? RefreshToken);

public sealed class RefreshSessionHandler(IUserRepository users, ISessionRepository sessions, IAuditLog audit, TimeProvider time)
{
    public async Task<SessionResult> HandleAsync(RefreshSessionCommand command, CancellationToken ct)
    {
        if (!RefreshToken.TryParse(command.RefreshToken, out SessionId sessionId, out string? secret)
            || await sessions.FindAsync(sessionId, ct) is not { } session)
        {
            throw new UnauthorizedException();
        }

        // A password reset, deactivation or role change since sign-in ends the session.
        User? user = await users.FindAsync(session.UserId, ct);
        if (user is not { Status: UserStatus.Active } || user.SecurityStamp != session.SecurityStamp)
        {
            throw new UnauthorizedException();
        }

        DateTimeOffset now = time.GetUtcNow();
        (RefreshOutcome outcome, RefreshToken? token) = session.Refresh(secret, now);
        switch (outcome)
        {
            case RefreshOutcome.Reused:
                await sessions.RevokeAllAsync(session.UserId, now, ct);
                audit.RefreshTokenReused(session.UserId);
                throw new UnauthorizedException();
            case RefreshOutcome.Rejected:
                throw new UnauthorizedException();
            default:
                await sessions.SaveAsync(session, ct);
                return session.ToResult(token!);
        }
    }
}
