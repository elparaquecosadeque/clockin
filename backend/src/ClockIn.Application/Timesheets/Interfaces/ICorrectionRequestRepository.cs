using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Interfaces;

public interface ICorrectionRequestRepository
{
    Task<CorrectionRequest?> FindAsync(CorrectionRequestId id, CancellationToken ct);

    Task<CorrectionRequest?> FindPendingAsync(TimesheetId timesheetId, CancellationToken ct);

    /// <summary>The most recent request on the timesheet, decided or not.</summary>
    Task<CorrectionRequest?> FindLatestAsync(TimesheetId timesheetId, CancellationToken ct);

    /// <summary>Inserts or updates the request. A second pending one conflicts with CorrectionRequestAlreadyPending.</summary>
    Task SaveOrUpdateAsync(CorrectionRequest request, CancellationToken ct);
}
