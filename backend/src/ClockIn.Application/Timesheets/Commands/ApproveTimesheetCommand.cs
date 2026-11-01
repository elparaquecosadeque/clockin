using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Final for employees and supervisors. Two approvers at once: the version check lets one win.</summary>
public sealed record ApproveTimesheetCommand(Guid TimesheetId) : ITransactional;

public sealed class ApproveTimesheetHandler(
    ICurrentUser currentUser,
    TimesheetAccess timesheetAccess,
    ITimesheetRepository timesheetsRepository,
    ICorrectionRequestRepository correctionsRepository,
    TimeProvider time)
{
    public async Task HandleAsync(ApproveTimesheetCommand command, CancellationToken ct)
    {
        Timesheet timesheet = await timesheetsRepository.GetAsync(command.TimesheetId, ct);
        TimesheetParties parties = await timesheetAccess.PartiesAsync(timesheet.OwnerId, ct);
        TimesheetParties.Require(parties.IsApprover);
        await correctionsRepository.EnsureNonePendingAsync(timesheet.Id, ct);

        timesheet.Approve(currentUser.Id, parties.Owner.TeamId, time.GetUtcNow());
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
    }
}
