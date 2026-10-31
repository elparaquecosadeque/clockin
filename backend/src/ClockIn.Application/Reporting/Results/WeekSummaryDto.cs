namespace ClockIn.Application.Reporting.Results;

public sealed record WeekSummaryDto(int NotStarted, int Draft, int Submitted, int Approved);
