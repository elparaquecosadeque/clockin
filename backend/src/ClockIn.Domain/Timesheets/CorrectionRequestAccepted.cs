using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;

namespace ClockIn.Domain.Timesheets;

/// <summary>Reopens the timesheet to Draft in the same transaction.</summary>
public sealed record CorrectionRequestAccepted(
    CorrectionRequestId RequestId, TimesheetId TimesheetId, UserId ApproverId, string? Comment, DateTimeOffset At) : IDomainEvent;
