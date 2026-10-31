using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;
using ClockIn.Persistence.Common;
using LinqToDB;
using LinqToDB.Async;

namespace ClockIn.Persistence.Accounts;

public sealed class UserRepository(AppDataConnection db) : IUserRepository
{
    public async Task<User?> FindAsync(UserId id, CancellationToken ct)
    {
        UserRecord? record = await db.GetTable<UserRecord>().FirstOrDefaultAsync(u => u.Id == id.Value, ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task<User?> FindByEmailAsync(Email email, CancellationToken ct)
    {
        UserRecord? record = await db.GetTable<UserRecord>().FirstOrDefaultAsync(u => u.Email == email.Value, ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<User>> FindByRoleAsync(Role role, CancellationToken ct)
    {
        string name = role.ToString();
        List<UserRecord> records = await db.GetTable<UserRecord>().Where(u => u.Role == name).ToListAsync(ct);
        return records.ConvertAll(ToDomain);
    }

    public async Task<IReadOnlyDictionary<UserId, string>> NamesAsync(IEnumerable<UserId> ids, CancellationToken ct)
    {
        Guid[] wanted = [.. ids.Select(id => id.Value).Distinct()];
        if (wanted.Length == 0)
        {
            return new Dictionary<UserId, string>();
        }

        return await db.GetTable<UserRecord>()
            .Where(u => wanted.Contains(u.Id))
            .ToDictionaryAsync(u => new UserId(u.Id), u => u.Name, ct);
    }

    public async Task SaveAsync(User user, CancellationToken ct)
    {
        if (user.Version == 0)
        {
            try
            {
                await db.InsertAsync(ToRecord(user, version: 1), token: ct);
            }
            catch (Exception ex) when (UniqueViolation.Is(ex))
            {
                // The only unique index besides the key.
                throw new ConflictException("EmailAlreadyInUse", "Another user already has this email.");
            }

            return;
        }

        int updated = await db.GetTable<UserRecord>()
            .Where(u => u.Id == user.Id.Value && u.Version == user.Version)
            .Set(u => u.Status, user.Status.ToString())
            .Set(u => u.PasswordHash, user.PasswordHash)
            .Set(u => u.SecurityStamp, user.SecurityStamp)
            .Set(u => u.FailedSignIns, user.FailedSignIns)
            .Set(u => u.LockedUntil, user.LockedUntil)
            .Set(u => u.AccessLinkHash, user.AccessLinkHash)
            .Set(u => u.AccessLinkExpiresAt, user.AccessLinkExpiresAt)
            .Set(u => u.TeamId, user.TeamId == null ? null : user.TeamId.Value.Value)
            .Set(u => u.Version, user.Version + 1)
            .UpdateAsync(ct);
        if (updated == 0)
        {
            throw new ConflictException("ConcurrencyConflict", "The user was changed by someone else. Reload and try again.");
        }
    }

    private static User ToDomain(UserRecord record) =>
        User.Restore(
            new UserId(record.Id),
            new Email(record.Email),
            record.Name,
            Enum.Parse<Role>(record.Role),
            Enum.Parse<UserStatus>(record.Status),
            record.PasswordHash,
            record.SecurityStamp,
            record.FailedSignIns,
            record.LockedUntil,
            record.AccessLinkHash,
            record.AccessLinkExpiresAt,
            record.TeamId is { } teamId ? new TeamId(teamId) : null,
            record.Version);

    private static UserRecord ToRecord(User user, int version) => new()
    {
        Id = user.Id.Value,
        Email = user.Email.Value,
        Name = user.Name,
        Role = user.Role.ToString(),
        Status = user.Status.ToString(),
        PasswordHash = user.PasswordHash,
        SecurityStamp = user.SecurityStamp,
        FailedSignIns = user.FailedSignIns,
        LockedUntil = user.LockedUntil,
        AccessLinkHash = user.AccessLinkHash,
        AccessLinkExpiresAt = user.AccessLinkExpiresAt,
        TeamId = user.TeamId?.Value,
        Version = version,
    };
}
