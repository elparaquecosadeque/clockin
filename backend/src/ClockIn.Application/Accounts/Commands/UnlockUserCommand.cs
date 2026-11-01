using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Admins only (permission policy). Unlocking someone who isn't locked changes nothing.</summary>
public sealed record UnlockUserCommand(Guid UserId);

public sealed class UnlockUserHandler(
    ICurrentUser currentUser,
    IUserRepository usersRepository,
    IAuditLog audit)
{
    public async Task HandleAsync(UnlockUserCommand command, CancellationToken ct)
    {
        User user = await usersRepository.FindAsync(new UserId(command.UserId), ct) ?? throw new NotFoundException("User not found.");
        user.Unlock();
        await usersRepository.SaveOrUpdateAsync(user, ct);
        audit.UserUnlocked(currentUser.Id, user.Id);
    }
}
