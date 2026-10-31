namespace ClockIn.Application.Reporting;

/// <summary>One closed entry of an approved timesheet, as the payroll file shows it.</summary>
public sealed record ApprovedHoursLine(
    string Employee, string Email, string? Team, DateOnly WeekStart, DateTimeOffset Start, DateTimeOffset End, bool ManuallyChanged, string? Note);
