using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Reporting.Interfaces;

public sealed record CorrectionRow(
    Guid Id, Guid TimesheetId, Guid OwnerId, DateOnly WeekStart, CorrectionStatus Status, string Reason, DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt);
