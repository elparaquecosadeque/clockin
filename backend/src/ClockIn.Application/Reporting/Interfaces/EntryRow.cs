namespace ClockIn.Application.Reporting.Interfaces;

public sealed record EntryRow(Guid Id, DateTimeOffset Start, DateTimeOffset? End, bool IsManuallyChanged, string? Note);
