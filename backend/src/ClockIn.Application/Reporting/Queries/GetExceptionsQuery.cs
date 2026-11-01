using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Reporting.Queries;

/// <summary>
/// Overdue Drafts of ended weeks, long entries and many manual changes. Approved weeks are settled, so only their
/// overdue state could matter, and they have none.
/// </summary>
public sealed record GetExceptionsQuery;

public sealed class GetExceptionsHandler(
    ICurrentUser currentUser,
    IReportingReads reads,
    TimeProvider time)
{
    public async Task<IReadOnlyList<ExceptionDto>> HandleAsync(GetExceptionsQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        scope.RequireReview();
        var people = scope.Reviewees.ToDictionary(p => p.Id);
        DateTimeOffset now = time.GetUtcNow();

        List<ExceptionDto> found = [];
        foreach (TimesheetRow timesheet in await reads.TimesheetsAsync(people.Keys, null, ct))
        {
            if (timesheet.Status == TimesheetStatus.Approved)
            {
                continue;
            }

            PersonRow owner = people[timesheet.OwnerId];
            ExceptionDto Exception(ExceptionKind kind, Guid? entryId = null, long? seconds = null, int? count = null) =>
                new(kind, owner.Id, owner.Name, scope.Team(owner.TeamId)?.Name, timesheet.WeekStart, timesheet.Id, entryId, seconds, count);

            if (timesheet.Status == TimesheetStatus.Draft && WorkedTimeHelper.HasEnded(timesheet.WeekStart, now, time.LocalTimeZone))
            {
                found.Add(Exception(ExceptionKind.OverdueDraft));
            }

            found.AddRange(timesheet.Entries
                .Where(e => e.End is not null && e.End - e.Start > WorkedTimeHelper.LongEntry)
                .Select(e => Exception(ExceptionKind.LongEntry, e.Id, WorkedTimeHelper.Seconds(e, now))));

            if (timesheet.ManuallyChangedEntries >= WorkedTimeHelper.ManyManualChanges)
            {
                found.Add(Exception(ExceptionKind.ManyManualChanges, count: timesheet.ManuallyChangedEntries));
            }
        }

        return [.. found.OrderByDescending(e => e.WeekStart).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(e => e.Kind)];
    }
}
