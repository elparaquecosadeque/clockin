using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Mappings;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using FluentValidation;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>
/// Ends every session and starts a fresh one for the device that asked. Not <see cref="ITransactional"/>: a wrong
/// current password is saved as a failed attempt before the rule is broken, and a rollback would lose it.
/// </summary>
public sealed record ChangeOwnPasswordCommand(string CurrentPassword, string NewPassword);

public sealed class ChangeOwnPasswordValidator : AbstractValidator<ChangeOwnPasswordCommand>
{
    public ChangeOwnPasswordValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword).NotEmpty()
            .Length(SetPasswordWithLinkValidator.MinPasswordLength, SetPasswordWithLinkValidator.MaxPasswordLength);
    }
}

public sealed class ChangeOwnPasswordHandler(
    ICurrentUser currentUser,
    IUserRepository usersRepository,
    ISessionRepository sessionsRepository,
    IPasswordHasher hasher,
    IAuditLog audit,
    TimeProvider time)
{
    public async Task<SessionResult> HandleAsync(ChangeOwnPasswordCommand command, CancellationToken ct)
    {
        User user = await usersRepository.FindAsync(currentUser.Id, ct) ?? throw new UnauthorizedException();
        bool matches = hasher.Verify(user.PasswordHash, command.CurrentPassword);
        DateTimeOffset now = time.GetUtcNow();
        PasswordChangeOutcome outcome = user.ChangePassword(matches, matches ? hasher.Hash(command.NewPassword) : "", now);
        await usersRepository.SaveOrUpdateAsync(user, ct);
        if (outcome == PasswordChangeOutcome.LockedOut)
        {
            await sessionsRepository.RevokeAllAsync(user.Id, now, ct);
            audit.LockedOut(user.Id);
        }

        if (outcome != PasswordChangeOutcome.Changed)
        {
            throw new DomainException("InvalidCurrentPassword", "The current password is incorrect.");
        }

        await sessionsRepository.RevokeAllAsync(user.Id, now, ct);
        (Session session, RefreshToken token) = Session.Start(user.Id, user.SecurityStamp, now);
        await sessionsRepository.SaveOrUpdateAsync(session, ct);
        return session.ToResult(token);
    }
}
