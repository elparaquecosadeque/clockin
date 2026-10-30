using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Domain.Accounts;
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
        Version = version,
    };
}
