using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Reporting;

/// <summary>Hours count on the company day an entry started; an open entry runs until now (glossary).</summary>
public static class WorkedTime
{
    /// <summary>Entries longer than this are worth a look (exceptions.feature).</summary>
    public static readonly TimeSpan LongEntry = TimeSpan.FromHours(10);

    /// <summary>A timesheet with this many hand-changed entries is worth a look (exceptions.feature).</summary>
    public const int ManyManualChanges = 3;

    public static long Seconds(EntryRow entry, DateTimeOffset now) => (long)((entry.End ?? now) - entry.Start).TotalSeconds;

    public static DateOnly Day(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>Seconds per day, Monday first.</summary>
    public static long[] PerDay(IEnumerable<EntryRow> entries, DateOnly weekStart, DateTimeOffset now, TimeZoneInfo zone)
    {
        long[] days = new long[7];
        foreach (EntryRow entry in entries)
        {
            int index = Day(entry.Start, zone).DayNumber - weekStart.DayNumber;
            if (index is >= 0 and < 7)
            {
                days[index] += Seconds(entry, now);
            }
        }

        return days;
    }

    public static bool HasEnded(DateOnly weekStart, DateTimeOffset now, TimeZoneInfo zone) => now >= new Week(weekStart).EndsAt(zone);
}
