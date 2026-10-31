using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Application.Timesheets.Mappings;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Closes the open entry now, even when it started in an earlier week.</summary>
public sealed record ClockOutCommand(string IpAddress, string UserAgent) : ITransactional;

public sealed class ClockOutHandler(ICurrentUser currentUser, ITimesheetRepository timesheetsRepository, TimeProvider time)
{
    public async Task<TimeEntryDto> HandleAsync(ClockOutCommand command, CancellationToken ct)
    {
        Timesheet timesheet = await timesheetsRepository.FindWithOpenEntryAsync(currentUser.Id, ct)
            ?? throw new DomainException("NoOpenEntry", "There is no open entry to clock out of.");
        TimeEntry entry = timesheet.OpenEntry!;

        timesheet.ClockOut(time.GetUtcNow(), new PunchSource(command.IpAddress, command.UserAgent));
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
        return entry.ToDto(timesheet.Id);
    }
}
