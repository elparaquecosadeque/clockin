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
    ICurrentUser currentUser,
    IUserRepository usersRepository,
    ITeamRepository teamsRepository,
    ITimesheetRepository timesheetsRepository,
    ICorrectionRequestRepository correctionsRepository,
    TimeProvider time)
{
    public async Task HandleAsync(ReturnTimesheetCommand command, CancellationToken ct)
    {
        Timesheet timesheet = await timesheetsRepository.GetAsync(command.TimesheetId, ct);
        TimesheetParties parties = await TimesheetParties.LoadAsync(usersRepository, teamsRepository, currentUser.Id, timesheet.OwnerId, ct);
        TimesheetParties.Require(parties.IsApprover);
        await correctionsRepository.EnsureNonePendingAsync(timesheet.Id, ct);

        timesheet.Return(currentUser.Id, command.Comment, time.GetUtcNow());
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
    }
}
