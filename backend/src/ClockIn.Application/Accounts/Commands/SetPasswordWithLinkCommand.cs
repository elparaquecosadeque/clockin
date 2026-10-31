using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using FluentValidation;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>Activates an account or resets its password. Anonymous: the link is the credential.</summary>
public sealed record SetPasswordWithLinkCommand(string Token, string Password);

public sealed class SetPasswordWithLinkValidator : AbstractValidator<SetPasswordWithLinkCommand>
{
    // Length only, no composition rules (set-password-with-link.feature).
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 128;

    public SetPasswordWithLinkValidator()
    {
        RuleFor(c => c.Token).NotEmpty();
        RuleFor(c => c.Password).NotEmpty().Length(MinPasswordLength, MaxPasswordLength);
    }
}

public sealed class SetPasswordWithLinkHandler(IUserRepository usersRepository, IPasswordHasher hasher, TimeProvider time)
{
    public async Task HandleAsync(SetPasswordWithLinkCommand command, CancellationToken ct)
    {
        if (!AccessLink.TryParse(command.Token, out UserId userId, out string? secret)
            || await usersRepository.FindAsync(userId, ct) is not { } user)
        {
            throw new DomainException("InvalidOrExpiredLink", "The link is invalid or has expired. Ask an admin for a new one.");
        }

        user.SetPasswordWithLink(secret, hasher.Hash(command.Password), time.GetUtcNow());
        await usersRepository.SaveOrUpdateAsync(user, ct);
    }
}
