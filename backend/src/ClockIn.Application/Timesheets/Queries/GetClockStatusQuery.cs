using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Application.Timesheets.Mappings;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Queries;

public sealed record GetClockStatusQuery;

public sealed class GetClockStatusHandler(ICurrentUser currentUser, ITimesheetRepository timesheets, TimeProvider time)
{
    // ponytail: reads through the repository; switch to an ITimesheetReads projection when reporting needs one.
    public async Task<ClockStatusDto> HandleAsync(GetClockStatusQuery query, CancellationToken ct)
    {
        TimeZoneInfo zone = time.LocalTimeZone;
        DateTimeOffset now = time.GetUtcNow();
        var week = Week.Containing(now, zone);
        DateOnly today = CompanyDay(now, zone);

        Timesheet? withOpen = await timesheets.FindWithOpenEntryAsync(currentUser.Id, ct);
        Timesheet? thisWeek = withOpen?.Week == week ? withOpen : await timesheets.FindAsync(currentUser.Id, week, ct);
        IReadOnlyList<TimeEntry> entries = thisWeek?.Entries ?? [];
        TimeEntry? open = withOpen?.OpenEntry;

        // Hours count on the day an entry started (glossary).
        var todays = entries.Where(e => CompanyDay(e.Start, zone) == today).ToList();
        return new ClockStatusDto(
            open?.ToDto(withOpen!.Id),
            open is not null && CompanyDay(open.Start, zone) == today,
            open is not null && week.Contains(open.Start, zone),
            todays.ConvertAll(e => e.ToDto(thisWeek!.Id)),
            ClosedSeconds(todays),
            ClosedSeconds(entries),
            week.Start,
            zone.Id);
    }

    private static DateOnly CompanyDay(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    private static long ClosedSeconds(IEnumerable<TimeEntry> entries) =>
        (long)entries.Sum(e => e.Duration?.TotalSeconds ?? 0);
}
