using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Teams.Interfaces;
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
    IUserRepository usersRepository,
    ITeamRepository teamsRepository,
    ITimesheetRepository timesheetsRepository,
    TimeProvider time)
{
    public async Task HandleAsync(DeleteEntryCommand command, CancellationToken ct)
    {
        var entryId = new TimeEntryId(command.EntryId);
        Timesheet timesheet = await timesheetsRepository.ForEntryChangeAsync(currentUser, usersRepository, teamsRepository, entryId, command.Version, ct);
        timesheet.DeleteEntry(currentUser.Id, entryId, command.Note, time.GetUtcNow());
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
    }
}
