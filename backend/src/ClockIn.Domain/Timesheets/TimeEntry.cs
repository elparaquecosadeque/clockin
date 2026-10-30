using ClockIn.Domain.Common;

namespace ClockIn.Domain.Timesheets;

/// <summary>A span of worked time inside a <see cref="Timesheet"/>.</summary>
public sealed class TimeEntry
{
    public static readonly TimeSpan MaxLength = TimeSpan.FromHours(16);

    internal TimeEntry(TimeEntryId id, DateTimeOffset start, PunchSource clockInSource)
    {
        Id = id;
        Start = start;
        ClockInSource = clockInSource;
    }

    public TimeEntryId Id { get; }

    public DateTimeOffset Start { get; }

    public DateTimeOffset? End { get; private set; }

    public PunchSource ClockInSource { get; }

    public PunchSource? ClockOutSource { get; private set; }

    public bool IsManuallyChanged { get; private set; }

    public TimeSpan? Duration => End - Start;

    /// <summary>Rebuilds a saved entry. For repositories only; it checks no rules.</summary>
    public static TimeEntry Restore(
        TimeEntryId id, DateTimeOffset start, DateTimeOffset? end, PunchSource clockInSource, PunchSource? clockOutSource, bool isManuallyChanged) =>
        new(id, start, clockInSource) { End = end, ClockOutSource = clockOutSource, IsManuallyChanged = isManuallyChanged };

    internal void ClockOut(DateTimeOffset end, PunchSource source)
    {
        if (end <= Start)
        {
            throw new DomainException("EntryHasNoLength", "An entry must end after it starts.");
        }

        if (end - Start > MaxLength)
        {
            throw new DomainException("EntryTooLong", $"An entry can't be longer than {MaxLength.TotalHours} hours. Set the end time by hand.");
        }

        End = end;
        ClockOutSource = source;
    }
}
