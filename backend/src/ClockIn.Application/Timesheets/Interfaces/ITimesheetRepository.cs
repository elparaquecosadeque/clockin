using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Interfaces;

public interface ITimesheetRepository
{
    Task<Timesheet?> FindAsync(TimesheetId id, CancellationToken ct);

    Task<Timesheet?> FindAsync(UserId ownerId, Week week, CancellationToken ct);

    /// <summary>The timesheet holding the entry.</summary>
    Task<Timesheet?> FindByEntryAsync(TimeEntryId entryId, CancellationToken ct);

    /// <summary>The owner's timesheet holding their open entry, whatever its week.</summary>
    Task<Timesheet?> FindWithOpenEntryAsync(UserId ownerId, CancellationToken ct);

    /// <summary>
    /// Makes other entry writes for <paramref name="ownerId"/> wait until this transaction ends. Outside a transaction
    /// it holds nothing.
    /// </summary>
    Task LockOwnerAsync(UserId ownerId, CancellationToken ct);

    /// <summary>Inserts or updates the whole aggregate and appends its new history.</summary>
    Task SaveOrUpdateAsync(Timesheet timesheet, CancellationToken ct);
}
