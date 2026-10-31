using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Interfaces;

public interface IUserRepository
{
    Task<User?> FindAsync(UserId id, CancellationToken ct);

    Task<User?> FindByEmailAsync(Email email, CancellationToken ct);

    Task<IReadOnlyList<User>> FindByRoleAsync(Role role, CancellationToken ct);

    /// <summary>Display names by id, for history and lists. Unknown ids are left out.</summary>
    Task<IReadOnlyDictionary<UserId, string>> NamesAsync(IEnumerable<UserId> ids, CancellationToken ct);

    /// <summary>Inserts or updates the whole aggregate.</summary>
    Task SaveOrUpdateAsync(User user, CancellationToken ct);
}
