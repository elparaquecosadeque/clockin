using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Reporting.Queries;

/// <summary>Submitted timesheets and pending correction requests waiting for the signed-in approver, oldest first.</summary>
public sealed record GetReviewQueueQuery;

public sealed class GetReviewQueueHandler(ICurrentUser currentUser, IReportingReads reads, TimeProvider time)
{
    public async Task<ReviewQueueDto> HandleAsync(GetReviewQueueQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        scope.RequireReview();
        return await BuildAsync(scope, reads, time.GetUtcNow(), ct);
    }

    internal static async Task<ReviewQueueDto> BuildAsync(ReportingScope scope, IReportingReads reads, DateTimeOffset now, CancellationToken ct)
    {
        var owners = scope.QueueOwners.ToDictionary(p => p.Id);
        IReadOnlyList<TimesheetRow> timesheets = await reads.TimesheetsAsync(owners.Keys, null, ct);
        IReadOnlyList<CorrectionRow> corrections = await reads.CorrectionsAsync(owners.Keys, ct);
        var byId = timesheets.ToDictionary(t => t.Id);

        return new ReviewQueueDto(
            [.. timesheets
                .Where(t => t.Status == TimesheetStatus.Submitted)
                .OrderBy(t => t.WeekStart).ThenBy(t => t.SubmittedAt)
                .Select(t => new QueuedTimesheetDto(
                    t.Id, t.OwnerId, owners[t.OwnerId].Name, scope.Team(owners[t.OwnerId].TeamId)?.Name, t.WeekStart,
                    t.Entries.Sum(e => WorkedTime.Seconds(e, now)), t.SubmittedAt))],
            [.. corrections
                .Where(c => c.Status == CorrectionStatus.Pending)
                .OrderBy(c => c.RequestedAt)
                .Select(c => new QueuedCorrectionDto(
                    c.Id, c.TimesheetId, c.OwnerId, owners[c.OwnerId].Name, scope.Team(owners[c.OwnerId].TeamId)?.Name, c.WeekStart,
                    Enum.Parse<WeekStatus>(byId[c.TimesheetId].Status.ToString()), c.Reason, c.RequestedAt))]);
    }
}
