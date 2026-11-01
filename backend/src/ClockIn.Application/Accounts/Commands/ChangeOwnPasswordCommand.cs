using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Mappings;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
using FluentValidation;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Ends every session and starts a fresh one for the device that asked.</summary>
public sealed record ChangeOwnPasswordCommand(string CurrentPassword, string NewPassword) : ITransactional;

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
    TimeProvider time)
{
    public async Task<SessionResult> HandleAsync(ChangeOwnPasswordCommand command, CancellationToken ct)
    {
        User user = await usersRepository.FindAsync(currentUser.Id, ct) ?? throw new UnauthorizedException();
        bool matches = hasher.Verify(user.PasswordHash, command.CurrentPassword);
        user.ChangePassword(matches, matches ? hasher.Hash(command.NewPassword) : "");
        await usersRepository.SaveOrUpdateAsync(user, ct);

        DateTimeOffset now = time.GetUtcNow();
        await sessionsRepository.RevokeAllAsync(user.Id, now, ct);
        (Session session, RefreshToken token) = Session.Start(user.Id, user.SecurityStamp, now);
        await sessionsRepository.SaveOrUpdateAsync(session, ct);
        return session.ToResult(token);
    }
}
