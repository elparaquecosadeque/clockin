using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;

namespace ClockIn.Domain.Timesheets;

/// <summary>A span of worked time inside a <see cref="Timesheet"/>.</summary>
public sealed class TimeEntry
{
    public static readonly TimeSpan MaxLength = TimeSpan.FromHours(16);
    public const int MaxNoteLength = 500;

    internal TimeEntry(TimeEntryId id, DateTimeOffset start, PunchSource clockInSource)
    {
        Id = id;
        Start = start;
        ClockInSource = clockInSource;
    }

    public TimeEntryId Id { get; }

    public DateTimeOffset Start { get; private set; }

    public DateTimeOffset? End { get; private set; }

    /// <summary>Where the entry was punched or, for a manual one, where it was added from.</summary>
    public PunchSource ClockInSource { get; }

    public PunchSource? ClockOutSource { get; private set; }

    public ManualChange ManualChange { get; private set; }

    /// <summary>Who last added or edited the entry by hand.</summary>
    public UserId? ChangedBy { get; private set; }

    /// <summary>The note of the last manual change.</summary>
    public string? Note { get; private set; }

    /// <summary>The times as punched, kept forever once a punched entry is edited.</summary>
    public DateTimeOffset? PunchedStart { get; private set; }

    public DateTimeOffset? PunchedEnd { get; private set; }

    public bool IsManuallyChanged => ManualChange != ManualChange.None;

    public TimeSpan? Duration => End - Start;

    internal EntrySnapshot Snapshot => new(Id, Start, End, PunchedStart, PunchedEnd);

    /// <summary>Rebuilds a saved entry. For repositories only; it checks no rules.</summary>
    public static TimeEntry Restore(
        TimeEntryId id, DateTimeOffset start, DateTimeOffset? end, PunchSource clockInSource, PunchSource? clockOutSource,
        ManualChange manualChange, UserId? changedBy, string? note, DateTimeOffset? punchedStart, DateTimeOffset? punchedEnd) =>
        new(id, start, clockInSource)
        {
            End = end,
            ClockOutSource = clockOutSource,
            ManualChange = manualChange,
            ChangedBy = changedBy,
            Note = note,
            PunchedStart = punchedStart,
            PunchedEnd = punchedEnd,
        };

    internal static TimeEntry Manual(UserId actor, DateTimeOffset start, DateTimeOffset end, string note, PunchSource source) =>
        new(TimeEntryId.New(), start, source)
        {
            End = end,
            ClockOutSource = source,
            ManualChange = ManualChange.Added,
            ChangedBy = actor,
            Note = note,
        };

    internal void ClockOut(DateTimeOffset end, PunchSource source)
    {
        CheckLength(Start, end);
        End = end;
        ClockOutSource = source;
    }

    internal void Change(UserId actor, DateTimeOffset start, DateTimeOffset? end, string note)
    {
        if (ManualChange == ManualChange.None)
        {
            ManualChange = ManualChange.Edited;
            PunchedStart = Start;
            PunchedEnd = End;
        }

        Start = start;
        End = end;
        ChangedBy = actor;
        Note = note;
    }

    internal static void CheckLength(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new DomainException("EntryHasNoLength", "An entry must end after it starts.");
        }

        if (end - start > MaxLength)
        {
            throw new DomainException("EntryTooLong", $"An entry can't be longer than {MaxLength.TotalHours} hours. Set the end time by hand.");
        }
    }
}
