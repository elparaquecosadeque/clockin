using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Domain.Accounts;
using ClockIn.Persistence.Common;
using LinqToDB;
using LinqToDB.Async;

namespace ClockIn.Persistence.Accounts;

public sealed class SessionRepository(AppDataConnection db) : ISessionRepository
{
    public async Task<Session?> FindAsync(SessionId id, CancellationToken ct)
    {
        SessionRecord? record = await db.GetTable<SessionRecord>().FirstOrDefaultAsync(s => s.Id == id.Value, ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task SaveOrUpdateAsync(Session session, CancellationToken ct)
    {
        if (session.Version == 0)
        {
            await db.InsertAsync(ToRecord(session, version: 1), token: ct);
            return;
        }

        int updated = await db.GetTable<SessionRecord>()
            .Where(s => s.Id == session.Id.Value && s.Version == session.Version)
            .Set(s => s.TokenHash, session.TokenHash)
            .Set(s => s.ExpiresAt, session.ExpiresAt)
            .Set(s => s.RevokedAt, session.RevokedAt)
            .Set(s => s.Version, session.Version + 1)
            .UpdateAsync(ct);
        if (updated == 0)
        {
            // Two refreshes with the same token at once: only one may rotate it.
            throw new UnauthorizedException();
        }
    }

    public Task RevokeAllAsync(UserId userId, DateTimeOffset now, CancellationToken ct) =>
        db.GetTable<SessionRecord>()
            .Where(s => s.UserId == userId.Value && s.RevokedAt == null)
            .Set(s => s.RevokedAt, now)
            .Set(s => s.Version, s => s.Version + 1)
            .UpdateAsync(ct);

    private static Session ToDomain(SessionRecord record) =>
        Session.Restore(
            new SessionId(record.Id),
            new UserId(record.UserId),
            record.SecurityStamp,
            record.TokenHash,
            record.ExpiresAt,
            record.RevokedAt,
            record.Version);

    private static SessionRecord ToRecord(Session session, int version) => new()
    {
        Id = session.Id.Value,
        UserId = session.UserId.Value,
        SecurityStamp = session.SecurityStamp,
        TokenHash = session.TokenHash,
        ExpiresAt = session.ExpiresAt,
        RevokedAt = session.RevokedAt,
        Version = version,
    };
}
