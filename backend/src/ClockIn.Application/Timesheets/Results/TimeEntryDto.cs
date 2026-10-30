namespace ClockIn.Application.Timesheets.Results;

public sealed record TimeEntryDto(Guid Id, Guid TimesheetId, DateTimeOffset Start, DateTimeOffset? End, bool IsManuallyChanged);
