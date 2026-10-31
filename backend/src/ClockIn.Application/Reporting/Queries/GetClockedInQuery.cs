using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Reporting.Queries;

/// <summary>Who has an open entry now: a supervisor's team, or everyone for admins.</summary>
public sealed record GetClockedInQuery;

public sealed class GetClockedInHandler(ICurrentUser currentUser, IReportingReads reads, TimeProvider time)
{
    public async Task<IReadOnlyList<ClockedInDto>> HandleAsync(GetClockedInQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        scope.RequireReview();
        var people = scope.Reviewees.ToDictionary(p => p.Id);
        DateTimeOffset now = time.GetUtcNow();

        IReadOnlyList<TimesheetRow> timesheets = await reads.TimesheetsAsync(people.Keys, null, ct);
        return [.. timesheets
            .SelectMany(t => t.Entries.Where(e => e.End is null).Select(e => (t.OwnerId, Entry: e)))
            .Select(open => new ClockedInDto(
                open.OwnerId,
                people[open.OwnerId].Name,
                scope.Team(people[open.OwnerId].TeamId)?.Name,
                open.Entry.Start,
                WorkedTime.Seconds(open.Entry, now),
                now - open.Entry.Start > TimeEntry.MaxLength))
            .OrderBy(c => c.Since)];
    }
}
