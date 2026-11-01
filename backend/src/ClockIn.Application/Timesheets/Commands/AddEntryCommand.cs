using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>A forgotten entry. <see cref="OwnerId"/> is for admins adding to someone else's week; the note is then the reason.</summary>
public sealed record AddEntryCommand(Guid? OwnerId, DateTimeOffset Start, DateTimeOffset End, string Note, string IpAddress, string UserAgent)
    : ITransactional;

public sealed class AddEntryValidator : AbstractValidator<AddEntryCommand>
{
    public AddEntryValidator() => RuleFor(c => c.Note).MustBeNote();
}

public sealed class AddEntryHandler(
    ICurrentUser currentUser,
    TimesheetAccess timesheetAccess,
    ITimesheetRepository timesheetsRepository,
    TimeProvider time)
{
    public async Task<Guid> HandleAsync(AddEntryCommand command, CancellationToken ct)
    {
        UserId ownerId = command.OwnerId is { } id ? new UserId(id) : currentUser.Id;
        TimesheetParties parties = await timesheetAccess.PartiesAsync(ownerId, ct);
        TimesheetParties.Require(parties.CanChange);

        var week = Week.Containing(command.Start, time.LocalTimeZone);
        Timesheet timesheet = await timesheetsRepository.FindAsync(ownerId, week, ct) ?? Timesheet.Start(ownerId, week);
        TimeEntry entry = timesheet.AddEntry(
            currentUser.Id, command.Start, command.End, command.Note, new PunchSource(command.IpAddress, command.UserAgent),
            time.GetUtcNow(), time.LocalTimeZone, await timesheetsRepository.NeighboursAsync(ownerId, week, ct));
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
        return entry.Id.Value;
    }
}
