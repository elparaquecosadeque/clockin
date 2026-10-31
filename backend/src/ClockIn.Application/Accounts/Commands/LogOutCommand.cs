using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Ends the session of the given refresh token. Always succeeds, so it reveals nothing.</summary>
public sealed record LogOutCommand(string? RefreshToken);

public sealed class LogOutHandler(ISessionRepository sessionsRepository, TimeProvider time)
{
    public async Task HandleAsync(LogOutCommand command, CancellationToken ct)
    {
        if (RefreshToken.TryParse(command.RefreshToken, out SessionId sessionId, out string? secret)
            && await sessionsRepository.FindAsync(sessionId, ct) is { } session
            && session.Matches(secret))
        {
            session.Revoke(time.GetUtcNow());
            await sessionsRepository.SaveOrUpdateAsync(session, ct);
        }
    }
}
