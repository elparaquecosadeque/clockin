namespace ClockIn.Application.Reporting.Interfaces;

/// <summary>
/// Flat reads for reports, queues and exports. Handlers scope and shape them.
/// ponytail: loads whole tables for a small company; filter in SQL per report if they grow past a few thousand rows.
/// </summary>
public interface IReportingReads
{
    Task<IReadOnlyList<PersonRow>> PeopleAsync(CancellationToken ct);

    Task<IReadOnlyList<TeamRow>> TeamsAsync(CancellationToken ct);

    /// <summary>The owners' timesheets with their entries; one week, or every week when <paramref name="weekStart"/> is null.</summary>
    Task<IReadOnlyList<TimesheetRow>> TimesheetsAsync(IReadOnlyCollection<Guid> ownerIds, DateOnly? weekStart, CancellationToken ct);

    Task<IReadOnlyList<CorrectionRow>> CorrectionsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken ct);
}
