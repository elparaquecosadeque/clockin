using ClockIn.Application.Reporting.Results;

namespace ClockIn.Application.Reporting.Interfaces;

/// <summary>
/// Report-specific reads. Handlers supply authorized owner ids; filtering, aggregation and paging happen in SQL.
/// </summary>
public interface IReportingReads
{
    Task<IReadOnlyList<PersonRow>> PeopleAsync(CancellationToken ct);

    Task<IReadOnlyList<TeamRow>> TeamsAsync(CancellationToken ct);

    /// <summary>The owners' timesheets with their entries for one week only.</summary>
    Task<IReadOnlyList<TimesheetRow>> TimesheetsAsync(IReadOnlyCollection<Guid> ownerIds, DateOnly weekStart, CancellationToken ct);

    Task<ToDosDto> PersonalToDosAsync(Guid ownerId, DateOnly currentWeek, DateTimeOffset declinedSince, CancellationToken ct);

    Task<(int Timesheets, int Corrections)> ReviewCountsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken ct);

    Task<bool> HasPendingReviewsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken ct);

    Task<IReadOnlyList<ClockedInDto>> ClockedInAsync(IReadOnlyCollection<Guid> ownerIds, DateTimeOffset now, CancellationToken ct);

    Task<ReviewQueueDto> ReviewQueueAsync(IReadOnlyCollection<Guid> ownerIds, int timesheetPage, int correctionPage, DateTimeOffset now, CancellationToken ct);

    Task<ReportPageDto<ExceptionDto>> ExceptionsAsync(IReadOnlyCollection<Guid> ownerIds, DateOnly currentWeek, int page, CancellationToken ct);
}
