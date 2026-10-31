using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Admins only (permission policy). Unlocking someone who isn't locked changes nothing.</summary>
public sealed record UnlockUserCommand(Guid UserId);

public sealed class UnlockUserHandler(IUserRepository users)
{
    public async Task HandleAsync(UnlockUserCommand command, CancellationToken ct)
    {
        User user = await users.FindAsync(new UserId(command.UserId), ct) ?? throw new NotFoundException("User not found.");
        user.Unlock();
        await users.SaveAsync(user, ct);
    }
}
