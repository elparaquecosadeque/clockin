using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Interfaces;

public interface ITimesheetRepository
{
    Task<Timesheet?> FindAsync(UserId ownerId, Week week, CancellationToken ct);

    /// <summary>The owner's timesheet holding their open entry, whatever its week.</summary>
    Task<Timesheet?> FindWithOpenEntryAsync(UserId ownerId, CancellationToken ct);

    /// <summary>Inserts or updates the whole aggregate.</summary>
    Task SaveAsync(Timesheet timesheet, CancellationToken ct);
}
