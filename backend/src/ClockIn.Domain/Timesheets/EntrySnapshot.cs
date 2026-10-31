namespace ClockIn.Domain.Timesheets;

/// <summary>An entry's times at one moment of its history.</summary>
public sealed record EntrySnapshot(
    TimeEntryId EntryId, DateTimeOffset Start, DateTimeOffset? End, DateTimeOffset? PunchedStart, DateTimeOffset? PunchedEnd);
