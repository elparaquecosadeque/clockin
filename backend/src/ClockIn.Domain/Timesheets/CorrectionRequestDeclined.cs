using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;

namespace ClockIn.Domain.Timesheets;

public sealed record CorrectionRequestDeclined(
    CorrectionRequestId RequestId, TimesheetId TimesheetId, UserId ApproverId, string Comment, DateTimeOffset At) : IDomainEvent;
