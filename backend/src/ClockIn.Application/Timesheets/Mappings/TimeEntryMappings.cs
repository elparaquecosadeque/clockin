using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Mappings;

public static class TimeEntryMappings
{
    public static TimeEntryDto ToDto(this TimeEntry entry, TimesheetId timesheetId) =>
        new(entry.Id.Value, timesheetId.Value, entry.Start, entry.End, entry.IsManuallyChanged);
}
