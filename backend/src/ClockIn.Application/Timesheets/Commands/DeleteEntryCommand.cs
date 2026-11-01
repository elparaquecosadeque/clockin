using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Removes an entry from a Draft; the history keeps its last values. <see cref="Version"/> as for editing.</summary>
public sealed record DeleteEntryCommand(Guid EntryId, string Note, int Version) : ITransactional;

public sealed class DeleteEntryValidator : AbstractValidator<DeleteEntryCommand>
{
    public DeleteEntryValidator() => RuleFor(c => c.Note).MustBeNote();
}

public sealed class DeleteEntryHandler(
    ICurrentUser currentUser,
    TimesheetAccess timesheetAccess,
    ITimesheetRepository timesheetsRepository,
    TimeProvider time)
{
    public async Task HandleAsync(DeleteEntryCommand command, CancellationToken ct)
    {
        var entryId = new TimeEntryId(command.EntryId);
        Timesheet timesheet = await timesheetAccess.ForEntryChangeAsync(timesheetsRepository, entryId, command.Version, ct);
        timesheet.DeleteEntry(currentUser.Id, entryId, command.Note, time.GetUtcNow());
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
    }
}
