namespace ClockIn.Application.Timesheets.Results;

public sealed record ApprovalDto(string ApproverName, DateTimeOffset At);
