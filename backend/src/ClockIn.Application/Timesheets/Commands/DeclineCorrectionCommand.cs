using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
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
    ICurrentUser currentUser,
    TimesheetAccess timesheetAccess,
    ICorrectionRequestRepository correctionsRepository,
    TimeProvider time)
{
    public async Task HandleAsync(DeclineCorrectionCommand command, CancellationToken ct)
    {
        CorrectionRequest request = await timesheetAccess.ForApproverAsync(correctionsRepository, command.RequestId, ct);
        request.Decline(currentUser.Id, command.Comment, time.GetUtcNow());
        await correctionsRepository.SaveOrUpdateAsync(request, ct);
    }
}
