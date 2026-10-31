using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Leaves the timesheet as it was; the owner sees the comment.</summary>
public sealed record DeclineCorrectionCommand(Guid RequestId, string Comment) : ITransactional;

public sealed class DeclineCorrectionValidator : AbstractValidator<DeclineCorrectionCommand>
{
    public DeclineCorrectionValidator() => RuleFor(c => c.Comment).MustBeComment();
}

public sealed class DeclineCorrectionHandler(
    ICurrentUser currentUser, IUserRepository users, ITeamRepository teams, ICorrectionRequestRepository corrections, TimeProvider time)
{
    public async Task HandleAsync(DeclineCorrectionCommand command, CancellationToken ct)
    {
        CorrectionRequest request = await corrections.GetForApproverAsync(command.RequestId, currentUser, users, teams, ct);
        request.Decline(currentUser.Id, command.Comment, time.GetUtcNow());
        await corrections.SaveAsync(request, ct);
    }
}
