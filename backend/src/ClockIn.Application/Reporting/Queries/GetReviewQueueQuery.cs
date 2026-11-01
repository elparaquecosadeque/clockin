using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using FluentValidation;

namespace ClockIn.Application.Reporting.Queries;

public sealed record GetReviewQueueQuery(int TimesheetPage = 1, int CorrectionPage = 1);

public sealed class GetReviewQueueValidator : AbstractValidator<GetReviewQueueQuery>
{
    public GetReviewQueueValidator()
    {
        RuleFor(q => q.TimesheetPage).InclusiveBetween(1, ReportPageConstants.MaxPage);
        RuleFor(q => q.CorrectionPage).InclusiveBetween(1, ReportPageConstants.MaxPage);
    }
}

/// <summary>Pending work, oldest first, in independently paged lists.</summary>
public sealed class GetReviewQueueHandler(
    ICurrentUser currentUser,
    IReportingReads reads,
    TimeProvider time)
{
    public async Task<ReviewQueueDto> HandleAsync(GetReviewQueueQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        scope.RequireReview();
        return await reads.ReviewQueueAsync(
            scope.QueueOwners.Select(p => p.Id).ToArray(), query.TimesheetPage, query.CorrectionPage, time.GetUtcNow(), ct);
    }
}
