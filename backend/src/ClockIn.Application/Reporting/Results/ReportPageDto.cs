namespace ClockIn.Application.Reporting.Results;

public sealed record ReportPageDto<T>(IReadOnlyList<T> Items, bool HasMore);
