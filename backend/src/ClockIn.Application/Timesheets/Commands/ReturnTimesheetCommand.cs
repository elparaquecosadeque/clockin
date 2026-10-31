using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Back to Draft with what to fix. The approver never edits entries.</summary>
public sealed record ReturnTimesheetCommand(Guid TimesheetId, string Comment) : ITransactional;

public sealed class ReturnTimesheetValidator : AbstractValidator<ReturnTimesheetCommand>
{
    public ReturnTimesheetValidator() => RuleFor(c => c.Comment).MustBeComment();
}

public sealed class ReturnTimesheetHandler(
    ICurrentUser currentUser, IUserRepository users, ITeamRepository teams, ITimesheetRepository timesheets,
    ICorrectionRequestRepository corrections, TimeProvider time)
{
    public async Task HandleAsync(ReturnTimesheetCommand command, CancellationToken ct)
    {
        Timesheet timesheet = await timesheets.GetAsync(command.TimesheetId, ct);
        TimesheetParties parties = await TimesheetParties.LoadAsync(users, teams, currentUser.Id, timesheet.OwnerId, ct);
        TimesheetParties.Require(parties.IsApprover);
        await corrections.EnsureNonePendingAsync(timesheet.Id, ct);

        timesheet.Return(currentUser.Id, command.Comment, time.GetUtcNow());
        await timesheets.SaveAsync(timesheet, ct);
    }
}
