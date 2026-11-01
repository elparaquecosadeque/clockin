using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Reporting.Queries;

/// <summary>The signed-in user's own to-dos plus, for approvers, their queue counts.</summary>
public sealed record GetToDosQuery;

public sealed class GetToDosHandler(
    ICurrentUser currentUser,
    IReportingReads reads,
    TimeProvider time)
{
    /// <summary>Declined requests stay on the badge this long (to-do-badges.feature).</summary>
    public static readonly TimeSpan DeclinedWindow = TimeSpan.FromDays(7);

    public async Task<ToDosDto> HandleAsync(GetToDosQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        DateTimeOffset now = time.GetUtcNow();
        Guid me = scope.Actor.Id;

        IReadOnlyList<TimesheetRow> mine = scope.Actor.Role.Can(Permissions.PunchTime) ? await reads.TimesheetsAsync([me], null, ct) : [];
        IReadOnlyList<CorrectionRow> myRequests = mine.Count > 0 ? await reads.CorrectionsAsync([me], ct) : [];
        List<TimesheetRow> drafts = [.. mine.Where(t => t.Status == TimesheetStatus.Draft).OrderBy(t => t.WeekStart)];
        ReviewQueueDto queue = scope.CanReview ? await GetReviewQueueHandler.BuildAsync(scope, reads, now, ct) : new([], []);

        return new ToDosDto(
            mine.SelectMany(t => t.Entries).FirstOrDefault(e => e.End is null)?.Start,
            [.. drafts.Where(t => t.ReturnComment is not null).Select(t => t.WeekStart)],
            [.. drafts.Where(t => t.ReturnComment is null && WorkedTime.HasEnded(t.WeekStart, now, time.LocalTimeZone)).Select(t => t.WeekStart)],
            myRequests.Count(r => r.Status == CorrectionStatus.Declined && r.DecidedAt >= now - DeclinedWindow),
            queue.Timesheets.Count,
            queue.Corrections.Count);
    }
}
