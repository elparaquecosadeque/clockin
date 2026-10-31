using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Results;

/// <summary>An entry with its manual-change flag. Punched values are set once a punched entry was edited.</summary>
public sealed record TimesheetEntryDto(
    Guid Id,
    DateOnly Date,
    DateTimeOffset Start,
    DateTimeOffset? End,
    long Seconds,
    ManualChange ManualChange,
    string? ChangedByName,
    string? Note,
    DateTimeOffset? PunchedStart,
    DateTimeOffset? PunchedEnd);
