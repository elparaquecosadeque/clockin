using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Teams.Interfaces;
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
    ICurrentUser currentUser, IUserRepository users, ITeamRepository teams, ITimesheetRepository timesheets,
    ICorrectionRequestRepository corrections, TimeProvider time)
{
    public async Task<Guid> HandleAsync(RequestCorrectionCommand command, CancellationToken ct)
    {
        Timesheet timesheet = await timesheets.GetAsync(command.TimesheetId, ct);
        TimesheetParties parties = await TimesheetParties.LoadAsync(users, teams, currentUser.Id, timesheet.OwnerId, ct);
        TimesheetParties.Require(parties.IsOwner && parties.CanChange);
        if (await corrections.FindPendingAsync(timesheet.Id, ct) is not null)
        {
            throw new ConflictException("CorrectionRequestAlreadyPending", "A correction request on this week is already waiting.");
        }

        // The timesheet records it, and rejects a Draft, through CorrectionRequested in this transaction.
        var request = CorrectionRequest.Create(timesheet.Id, timesheet.OwnerId, command.Reason, time.GetUtcNow());
        await corrections.SaveAsync(request, ct);
        return request.Id.Value;
    }
}
