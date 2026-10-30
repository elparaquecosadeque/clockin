namespace ClockIn.Application.Timesheets.Results;

/// <summary>
/// The clock screen's data. Totals cover closed entries only; the client adds the running open entry when the
/// flags say it counts, so its timer ticks without asking the server.
/// </summary>
public sealed record ClockStatusDto(
    TimeEntryDto? OpenEntry,
    bool OpenEntryStartedToday,
    bool OpenEntryStartedThisWeek,
    IReadOnlyList<TimeEntryDto> TodayEntries,
    long ClosedTodaySeconds,
    long ClosedWeekSeconds,
    DateOnly WeekStart,
    string TimeZone);
