namespace ClockIn.Application.Reporting.Results;

/// <summary>An entry's id and length for long entries; the count of changed entries for many manual changes.</summary>
public sealed record ExceptionDto(
    ExceptionKind Kind, Guid UserId, string Name, string? TeamName, DateOnly WeekStart, Guid TimesheetId, Guid? EntryId, long? Seconds, int? Count);
