namespace ClockIn.Application.Timesheets.Results;

/// <summary>What the signed-in user can do on this timesheet now, so the page shows only working actions.</summary>
public sealed record TimesheetActionsDto(
    bool ChangeEntries,
    bool Submit,
    bool Approve,
    bool Return,
    bool Reopen,
    bool RequestCorrection,
    bool DecideCorrection);
