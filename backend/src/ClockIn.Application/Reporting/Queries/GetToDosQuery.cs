using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Reporting.Queries;

public sealed record GetToDosQuery;

/// <summary>Badges use SQL counts, never a materialized review queue.</summary>
public sealed class GetToDosHandler(
    ICurrentUser currentUser,
    IReportingReads reads,
    TimeProvider time)
{
    public static readonly TimeSpan DeclinedWindow = TimeSpan.FromDays(7);

    public async Task<ToDosDto> HandleAsync(GetToDosQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        DateTimeOffset now = time.GetUtcNow();
        ToDosDto mine = scope.Actor.Role.Can(Permissions.PunchTime)
            ? await reads.PersonalToDosAsync(scope.Actor.Id, Week.Containing(now, time.LocalTimeZone).Start, now - DeclinedWindow, ct)
            : new(null, [], [], 0, 0, 0);
        (int timesheets, int corrections) = scope.CanReview
            ? await reads.ReviewCountsAsync(scope.QueueOwners.Select(p => p.Id).ToArray(), ct)
            : (0, 0);
        return mine with { TimesheetsToReview = timesheets, CorrectionsToDecide = corrections };
    }
}
