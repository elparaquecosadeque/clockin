namespace ClockIn.Application.Timesheets.Results;

public sealed record DayTotalDto(DateOnly Date, long Seconds, bool HasOpenEntry);
