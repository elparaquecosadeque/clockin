using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>An admin's direct Approved → Draft. Supervisors reopen only by accepting a correction request.</summary>
public sealed record ReopenTimesheetCommand(Guid TimesheetId, string Reason) : ITransactional;

public sealed class ReopenTimesheetValidator : AbstractValidator<ReopenTimesheetCommand>
{
    public ReopenTimesheetValidator() => RuleFor(c => c.Reason).MustBeComment();
}

public sealed class ReopenTimesheetHandler(
    ICurrentUser currentUser,
    TimesheetAccess access,
    ITimesheetRepository timesheetsRepository,
    ICorrectionRequestRepository correctionsRepository,
    TimeProvider time)
{
    public async Task HandleAsync(ReopenTimesheetCommand command, CancellationToken ct)
    {
        Timesheet timesheet = await timesheetsRepository.GetAsync(command.TimesheetId, ct);
        TimesheetParties parties = await access.PartiesAsync(timesheet.OwnerId, ct);
        TimesheetParties.Require(parties.CanReopen);
        await correctionsRepository.EnsureNonePendingAsync(timesheet.Id, ct);

        timesheet.Reopen(currentUser.Id, command.Reason, time.GetUtcNow());
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
    }
}
