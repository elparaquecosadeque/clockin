namespace ClockIn.Application.Reporting.Results;

public sealed record TeamWeekDto(DateOnly WeekStart, IReadOnlyList<TeamWeekGroupDto> Groups, string TimeZone);
