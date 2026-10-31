namespace ClockIn.Application.Reporting.Results;

public sealed record QueuedTimesheetDto(
    Guid TimesheetId, Guid OwnerId, string Name, string? TeamName, DateOnly WeekStart, long TotalSeconds, DateTimeOffset? SubmittedAt);
