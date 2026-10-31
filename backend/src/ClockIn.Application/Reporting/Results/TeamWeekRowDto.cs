using ClockIn.Application.Timesheets.Results;

namespace ClockIn.Application.Reporting.Results;

/// <summary>Seven day totals, Monday first; <see cref="OpenDay"/> is the index of the day with a running entry.</summary>
public sealed record TeamWeekRowDto(
    Guid UserId, string Name, WeekStatus Status, Guid? TimesheetId, IReadOnlyList<long> DaySeconds, int? OpenDay, long TotalSeconds);
