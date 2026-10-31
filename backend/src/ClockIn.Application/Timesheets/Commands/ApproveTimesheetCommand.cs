using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Final for employees and supervisors. Two approvers at once: the version check lets one win.</summary>
public sealed record ApproveTimesheetCommand(Guid TimesheetId) : ITransactional;

public sealed class ApproveTimesheetHandler(
    ICurrentUser currentUser, IUserRepository users, ITeamRepository teams, ITimesheetRepository timesheets,
    ICorrectionRequestRepository corrections, TimeProvider time)
{
    public async Task HandleAsync(ApproveTimesheetCommand command, CancellationToken ct)
    {
        Timesheet timesheet = await timesheets.GetAsync(command.TimesheetId, ct);
        TimesheetParties parties = await TimesheetParties.LoadAsync(users, teams, currentUser.Id, timesheet.OwnerId, ct);
        TimesheetParties.Require(parties.IsApprover);
        await corrections.EnsureNonePendingAsync(timesheet.Id, ct);

        timesheet.Approve(currentUser.Id, parties.Owner.TeamId, time.GetUtcNow());
        await timesheets.SaveAsync(timesheet, ct);
    }
}
