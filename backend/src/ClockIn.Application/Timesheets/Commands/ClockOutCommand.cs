using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Application.Timesheets.Mappings;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Closes the open entry now, even when it started in an earlier week.</summary>
public sealed record ClockOutCommand(string IpAddress, string UserAgent);

public sealed class ClockOutHandler(ICurrentUser currentUser, ITimesheetRepository timesheets, TimeProvider time)
{
    public async Task<TimeEntryDto> HandleAsync(ClockOutCommand command, CancellationToken ct)
    {
        Timesheet timesheet = await timesheets.FindWithOpenEntryAsync(currentUser.Id, ct)
            ?? throw new DomainException("NoOpenEntry", "There is no open entry to clock out of.");
        TimeEntry entry = timesheet.OpenEntry!;

        timesheet.ClockOut(time.GetUtcNow(), new PunchSource(command.IpAddress, command.UserAgent));
        await timesheets.SaveAsync(timesheet, ct);
        return entry.ToDto(timesheet.Id);
    }
}
