using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Interfaces;

public interface ISessionRepository
{
    Task<Session?> FindAsync(SessionId id, CancellationToken ct);

    /// <summary>Inserts or updates the whole aggregate.</summary>
    Task SaveOrUpdateAsync(Session session, CancellationToken ct);

    /// <summary>Ends every session of the user at once, as a bulk update.</summary>
    Task RevokeAllAsync(UserId userId, DateTimeOffset now, CancellationToken ct);
}
