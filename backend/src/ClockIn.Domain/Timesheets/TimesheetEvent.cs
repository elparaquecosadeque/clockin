using ClockIn.Domain.Accounts;

namespace ClockIn.Domain.Timesheets;

/// <summary>
/// One row of a timesheet's append-only history. An actor other than the owner on a transition the owner normally
/// makes, like Submitted, means it was done on the owner's behalf.
/// </summary>
public sealed record TimesheetEvent(
    TimesheetEventKind Kind, UserId ActorId, DateTimeOffset At, string? Comment, EntrySnapshot? Before, EntrySnapshot? After);
