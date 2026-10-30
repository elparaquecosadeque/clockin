using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Application.Timesheets.Mappings;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Commands;

/// <summary>Opens an entry now. The IP and user agent come from the request, not from the user.</summary>
public sealed record ClockInCommand(string IpAddress, string UserAgent);

public sealed class ClockInHandler(ICurrentUser currentUser, ITimesheetRepository timesheets, TimeProvider time)
{
    public async Task<TimeEntryDto> HandleAsync(ClockInCommand command, CancellationToken ct)
    {
        // The open entry may sit in an earlier week's timesheet, which this week's aggregate can't see.
        if (await timesheets.FindWithOpenEntryAsync(currentUser.Id, ct) is not null)
        {
            throw new DomainException("EntryAlreadyOpen", "An entry is already open. Clock out first.");
        }

        DateTimeOffset now = time.GetUtcNow();
        var week = Week.Containing(now, time.LocalTimeZone);
        Timesheet timesheet = await timesheets.FindAsync(currentUser.Id, week, ct) ?? Timesheet.Start(currentUser.Id, week);

        timesheet.ClockIn(now, new PunchSource(command.IpAddress, command.UserAgent), time.LocalTimeZone);
        await timesheets.SaveAsync(timesheet, ct);
        return timesheet.OpenEntry!.ToDto(timesheet.Id);
    }
}
