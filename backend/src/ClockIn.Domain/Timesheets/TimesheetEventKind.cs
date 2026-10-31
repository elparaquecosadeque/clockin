namespace ClockIn.Domain.Timesheets;

public enum TimesheetEventKind
{
    Submitted,
    Returned,
    Approved,
    Reopened,
    CorrectionRequested,
    CorrectionDeclined,
    EntryAdded,
    EntryEdited,
    EntryDeleted,
}
