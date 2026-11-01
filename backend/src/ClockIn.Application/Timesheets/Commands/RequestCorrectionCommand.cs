using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>The owner asks the approver to reopen a Submitted or Approved week.</summary>
public sealed record RequestCorrectionCommand(Guid TimesheetId, string Reason) : ITransactional;

public sealed class RequestCorrectionValidator : AbstractValidator<RequestCorrectionCommand>
{
    public RequestCorrectionValidator() => RuleFor(c => c.Reason).MustBeComment();
}

public sealed class RequestCorrectionHandler(
    TimesheetAccess timesheetAccess,
    ITimesheetRepository timesheetsRepository,
    ICorrectionRequestRepository correctionsRepository,
    TimeProvider time)
{
    public async Task<Guid> HandleAsync(RequestCorrectionCommand command, CancellationToken ct)
    {
        Timesheet timesheet = await timesheetsRepository.GetAsync(command.TimesheetId, ct);
        TimesheetParties parties = await timesheetAccess.PartiesAsync(timesheet.OwnerId, ct);
        TimesheetParties.Require(parties.IsOwner && parties.CanChange);
        if (await correctionsRepository.FindPendingAsync(timesheet.Id, ct) is not null)
        {
            throw new ConflictException("CorrectionRequestAlreadyPending", "A correction request on this week is already waiting.");
        }

        // The timesheet records it, and rejects a Draft, through CorrectionRequested in this transaction.
        var request = CorrectionRequest.Create(timesheet.Id, timesheet.OwnerId, command.Reason, time.GetUtcNow());
        await correctionsRepository.SaveOrUpdateAsync(request, ct);
        return request.Id.Value;
    }
}
