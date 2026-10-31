using ClockIn.Application.Timesheets.Results;

namespace ClockIn.Application.Reporting.Results;

public sealed record QueuedCorrectionDto(
    Guid RequestId, Guid TimesheetId, Guid OwnerId, string Name, string? TeamName, DateOnly WeekStart, WeekStatus TimesheetStatus,
    string Reason, DateTimeOffset RequestedAt);
