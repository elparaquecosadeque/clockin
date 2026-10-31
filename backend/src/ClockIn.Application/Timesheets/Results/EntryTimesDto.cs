namespace ClockIn.Application.Timesheets.Results;

public sealed record EntryTimesDto(Guid EntryId, DateTimeOffset Start, DateTimeOffset? End, DateTimeOffset? PunchedStart, DateTimeOffset? PunchedEnd);
