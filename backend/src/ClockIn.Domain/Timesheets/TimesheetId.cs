namespace ClockIn.Domain.Timesheets;

public readonly record struct TimesheetId(Guid Value)
{
    public static TimesheetId New() => new(Guid.CreateVersion7());
}
