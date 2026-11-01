using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;

namespace ClockIn.Application.Reporting.Queries;

public sealed record GetClockedInQuery;

/// <summary>Reads only open entries, at most one per authorized owner.</summary>
public sealed class GetClockedInHandler(
    ICurrentUser currentUser,
    IReportingReads reads,
    TimeProvider time)
{
    public async Task<IReadOnlyList<ClockedInDto>> HandleAsync(GetClockedInQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        scope.RequireReview();
        return await reads.ClockedInAsync(scope.Reviewees.Select(p => p.Id).ToArray(), time.GetUtcNow(), ct);
    }
}
