using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Domain.Accounts;
using FluentValidation;

namespace ClockIn.Application.Accounts.Commands;

/// <summary>The signed-in user's own language and time format (ADR 0001).</summary>
public sealed record UpdatePreferencesCommand(string Language, string TimeFormat);

public sealed class UpdatePreferencesValidator : AbstractValidator<UpdatePreferencesCommand>
{
    public UpdatePreferencesValidator()
    {
        RuleFor(c => c.Language).MustBeLanguage();
        RuleFor(c => c.TimeFormat).MustBeTimeFormat();
    }
}

public sealed class UpdatePreferencesHandler(ICurrentUser currentUser, IUserRepository users)
{
    public async Task HandleAsync(UpdatePreferencesCommand command, CancellationToken ct)
    {
        User user = await users.FindAsync(currentUser.Id, ct) ?? throw new UnauthorizedException();
        user.UpdatePreferences(command.Language, command.TimeFormat);
        await users.SaveAsync(user, ct);
    }
}
