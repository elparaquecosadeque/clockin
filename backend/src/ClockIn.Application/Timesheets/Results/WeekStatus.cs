namespace ClockIn.Application.Timesheets.Results;

/// <summary>A timesheet's status, or NotStarted for a week with no timesheet yet.</summary>
public enum WeekStatus
{
    NotStarted,
    Draft,
    Submitted,
    Approved,
}
