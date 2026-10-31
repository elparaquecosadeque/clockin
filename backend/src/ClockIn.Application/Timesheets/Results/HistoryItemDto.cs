using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Results;

/// <summary><see cref="ByOwner"/> false on a Submitted row means someone submitted on the owner's behalf.</summary>
public sealed record HistoryItemDto(
    TimesheetEventKind Kind, string ActorName, bool ByOwner, DateTimeOffset At, string? Comment, EntryTimesDto? Before, EntryTimesDto? After);
