using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Interfaces;

public interface IUserRepository
{
    Task<User?> FindAsync(UserId id, CancellationToken ct);

    Task<User?> FindByEmailAsync(Email email, CancellationToken ct);

    /// <summary>Inserts or updates the whole aggregate.</summary>
    Task SaveAsync(User user, CancellationToken ct);
}
