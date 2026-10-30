namespace ClockIn.Domain.Timesheets;

public readonly record struct TimeEntryId(Guid Value)
{
    public static TimeEntryId New() => new(Guid.CreateVersion7());
}
