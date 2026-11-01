using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Sends an ended week to its approver. <see cref="OwnerId"/> is for admins submitting on someone's behalf.</summary>
public sealed record SubmitTimesheetCommand(Guid? OwnerId, DateOnly WeekStart) : ITransactional;

public sealed class SubmitTimesheetValidator : AbstractValidator<SubmitTimesheetCommand>
{
    public SubmitTimesheetValidator() =>
        RuleFor(c => c.WeekStart).Must(d => d.DayOfWeek == DayOfWeek.Monday).WithMessage("A week starts on a Monday.");
}

public sealed class SubmitTimesheetHandler(
    ICurrentUser currentUser,
    TimesheetAccess timesheetAccess,
    ITimesheetRepository timesheetsRepository,
    TimeProvider time)
{
    public async Task HandleAsync(SubmitTimesheetCommand command, CancellationToken ct)
    {
        UserId ownerId = command.OwnerId is { } id ? new UserId(id) : currentUser.Id;
        TimesheetParties parties = await timesheetAccess.PartiesAsync(ownerId, ct);
        TimesheetParties.Require(parties.CanChange);

        var week = new Week(command.WeekStart);
        // A week with no entries can be submitted too: it then exists with 0 hours.
        Timesheet timesheet = await timesheetsRepository.FindAsync(ownerId, week, ct) ?? Timesheet.Start(ownerId, week);
        timesheet.Submit(currentUser.Id, time.GetUtcNow(), time.LocalTimeZone);
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
    }
}
