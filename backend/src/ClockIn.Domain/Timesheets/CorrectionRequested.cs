using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;

namespace ClockIn.Domain.Timesheets;

public sealed record CorrectionRequested(CorrectionRequestId RequestId, TimesheetId TimesheetId, UserId OwnerId, string Reason, DateTimeOffset At)
    : IDomainEvent;
