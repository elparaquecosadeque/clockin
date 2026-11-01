using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Reporting.Queries;

public sealed record GetExceptionsQuery(int Page = 1);

public sealed class GetExceptionsValidator : AbstractValidator<GetExceptionsQuery>
{
    public GetExceptionsValidator() => RuleFor(q => q.Page).InclusiveBetween(1, ReportPageConstants.MaxPage);
}

/// <summary>Unresolved exceptions, newest week first. Old work remains reachable through paging.</summary>
public sealed class GetExceptionsHandler(
    ICurrentUser currentUser,
    IReportingReads reads,
    TimeProvider time)
{
    public async Task<ReportPageDto<ExceptionDto>> HandleAsync(GetExceptionsQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        scope.RequireReview();
        DateOnly currentWeek = Week.Containing(time.GetUtcNow(), time.LocalTimeZone).Start;
        return await reads.ExceptionsAsync(scope.Reviewees.Select(p => p.Id).ToArray(), currentWeek, query.Page, ct);
    }
}
