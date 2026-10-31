using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Validation;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>A null <see cref="End"/> moves only the start of an open entry. <see cref="Version"/> is the timesheet's, as last read.</summary>
public sealed record EditEntryCommand(Guid EntryId, DateTimeOffset Start, DateTimeOffset? End, string Note, int Version) : ITransactional;

public sealed class EditEntryValidator : AbstractValidator<EditEntryCommand>
{
    public EditEntryValidator() => RuleFor(c => c.Note).MustBeNote();
}

public sealed class EditEntryHandler(
    ICurrentUser currentUser, IUserRepository usersRepository, ITeamRepository teamsRepository, ITimesheetRepository timesheetsRepository, TimeProvider time)
{
    public async Task HandleAsync(EditEntryCommand command, CancellationToken ct)
    {
        var entryId = new TimeEntryId(command.EntryId);
        Timesheet timesheet = await timesheetsRepository.ForEntryChangeAsync(currentUser, usersRepository, teamsRepository, entryId, command.Version, ct);
        timesheet.EditEntry(
            currentUser.Id, entryId, command.Start, command.End, command.Note, time.GetUtcNow(), time.LocalTimeZone,
            await timesheetsRepository.NeighboursAsync(timesheet.OwnerId, timesheet.Week, ct));
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
    }
}
