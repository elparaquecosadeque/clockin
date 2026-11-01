using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Mappings;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using FluentValidation;

namespace ClockIn.Application.Accounts.Commands;

public sealed record LogInCommand(string Email, string Password);

public sealed class LogInValidator : AbstractValidator<LogInCommand>
{
    public LogInValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Email.MaxLength);
        RuleFor(c => c.Password).NotEmpty();
    }
}

public sealed class LogInHandler(
    IUserRepository usersRepository,
    ISessionRepository sessionsRepository,
    IPasswordHasher hasher,
    IAuditLog audit,
    TimeProvider time)
{
    public async Task<SessionResult> HandleAsync(LogInCommand command, CancellationToken ct)
    {
        User? user = Email.TryCreate(command.Email, out Email? email) ? await usersRepository.FindByEmailAsync(email, ct) : null;
        // Always verify, even with no user, so timing doesn't reveal which emails exist.
        bool passwordMatches = hasher.Verify(user?.PasswordHash, command.Password);
        if (user is null)
        {
            throw Rejected(command.Email);
        }

        DateTimeOffset now = time.GetUtcNow();
        SignInOutcome outcome = user.SignIn(passwordMatches, now);
        // ponytail: the failure count is saved before throwing; a transaction-per-command middleware must not roll it back.
        try
        {
            await usersRepository.SaveOrUpdateAsync(user, ct);
        }
        catch (ConflictException)
        {
            // Another sign-in for this user saved first. A 409 only registered emails could get would reveal them.
            throw Rejected(command.Email);
        }

        if (outcome == SignInOutcome.LockedOut)
        {
            audit.LockedOut(user.Id);
        }

        if (outcome != SignInOutcome.SignedIn)
        {
            throw Rejected(command.Email);
        }

        (Session session, RefreshToken token) = Session.Start(user.Id, user.SecurityStamp, now);
        await sessionsRepository.SaveOrUpdateAsync(session, ct);
        audit.SignedIn(user.Id);
        return session.ToResult(token);
    }

    private DomainException Rejected(string email)
    {
        audit.SignInFailed(OneLine(email));
        return new DomainException("InvalidCredentials", "The email or password is incorrect.");
    }

    /// <summary>The email comes from an anonymous caller: line breaks in it would forge entries in a text log.</summary>
    private static string OneLine(string text) => string.Concat(text.Select(c => char.IsControl(c) ? '?' : c));
}
