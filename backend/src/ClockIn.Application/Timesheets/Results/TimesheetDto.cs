namespace ClockIn.Application.Timesheets.Results;

/// <summary>
/// One person's week in full. <see cref="Id"/> is null and <see cref="Version"/> 0 when the week has no timesheet yet.
/// Totals include an open entry up to now; hours count on the day an entry started.
/// </summary>
public sealed record TimesheetDto(
    Guid? Id,
    int Version,
    Guid OwnerId,
    string OwnerName,
    string? TeamName,
    DateOnly WeekStart,
    WeekStatus Status,
    string? ReturnComment,
    ApprovalDto? Approval,
    long TotalSeconds,
    IReadOnlyList<DayTotalDto> Days,
    IReadOnlyList<TimesheetEntryDto> Entries,
    IReadOnlyList<HistoryItemDto> History,
    CorrectionRequestDto? LatestCorrection,
    TimesheetActionsDto Can,
    string TimeZone);
