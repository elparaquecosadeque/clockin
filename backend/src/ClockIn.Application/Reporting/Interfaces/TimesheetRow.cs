using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Reporting.Interfaces;

/// <summary>
/// <see cref="ManuallyChangedEntries"/> counts distinct entries added, edited or deleted by hand.
/// <see cref="SubmittedAt"/> is the latest submission.
/// </summary>
public sealed record TimesheetRow(
    Guid Id,
    Guid OwnerId,
    DateOnly WeekStart,
    TimesheetStatus Status,
    string? ReturnComment,
    Guid? ApprovedTeamId,
    DateTimeOffset? SubmittedAt,
    int ManuallyChangedEntries,
    IReadOnlyList<EntryRow> Entries);
