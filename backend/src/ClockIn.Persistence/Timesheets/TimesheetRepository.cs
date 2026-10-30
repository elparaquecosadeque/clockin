using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using ClockIn.Persistence.Common;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;

namespace ClockIn.Persistence.Timesheets;

public sealed class TimesheetRepository(AppDataConnection db) : ITimesheetRepository
{
    public async Task<Timesheet?> FindAsync(UserId ownerId, Week week, CancellationToken ct)
    {
        TimesheetRecord? record = await db.GetTable<TimesheetRecord>()
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId.Value && t.WeekStart == week.Start, ct);
        return record is null ? null : await LoadAsync(record, ct);
    }

    public async Task<Timesheet?> FindWithOpenEntryAsync(UserId ownerId, CancellationToken ct)
    {
        TimesheetRecord? record = await (
            from entry in db.GetTable<TimeEntryRecord>()
            join timesheet in db.GetTable<TimesheetRecord>() on entry.TimesheetId equals timesheet.Id
            where entry.OwnerId == ownerId.Value && entry.EndedAt == null
            select timesheet).FirstOrDefaultAsync(ct);
        return record is null ? null : await LoadAsync(record, ct);
    }

    public async Task SaveAsync(Timesheet timesheet, CancellationToken ct)
    {
        // ponytail: one transaction per save; move it to the mediator's unit of work once a command saves more than one aggregate.
        await using DataConnectionTransaction transaction = await db.BeginTransactionAsync(ct);
        await SaveTimesheetAsync(timesheet, ct);
        await SaveEntriesAsync(timesheet, ct);
        await transaction.CommitAsync(ct);
    }

    private async Task SaveTimesheetAsync(Timesheet timesheet, CancellationToken ct)
    {
        try
        {
            if (timesheet.Version == 0)
            {
                await db.InsertAsync(ToRecord(timesheet, version: 1), token: ct);
                return;
            }

            int updated = await db.GetTable<TimesheetRecord>()
                .Where(t => t.Id == timesheet.Id.Value && t.Version == timesheet.Version)
                .Set(t => t.Status, timesheet.Status.ToString())
                .Set(t => t.Version, timesheet.Version + 1)
                .UpdateAsync(ct);
            if (updated == 0)
            {
                throw ConcurrencyConflict();
            }
        }
        catch (Exception ex) when (UniqueViolation.Is(ex))
        {
            // Someone created this person's timesheet for the week first.
            throw ConcurrencyConflict();
        }
    }

    private async Task SaveEntriesAsync(Timesheet timesheet, CancellationToken ct)
    {
        // The version check above already serialized writers of this timesheet, so rewriting its entries is safe.
        await db.GetTable<TimeEntryRecord>().Where(e => e.TimesheetId == timesheet.Id.Value).DeleteAsync(ct);
        try
        {
            foreach (TimeEntry entry in timesheet.Entries)
            {
                await db.InsertAsync(ToRecord(entry, timesheet), token: ct);
            }
        }
        catch (Exception ex) when (UniqueViolation.Is(ex))
        {
            // The only unique index on entries besides the key: an open entry already exists, possibly in another week.
            throw new ConflictException("EntryAlreadyOpen", "An entry is already open. Clock out first.");
        }
    }

    private async Task<Timesheet> LoadAsync(TimesheetRecord record, CancellationToken ct)
    {
        List<TimeEntryRecord> entries = await db.GetTable<TimeEntryRecord>()
            .Where(e => e.TimesheetId == record.Id)
            .OrderBy(e => e.StartedAt)
            .ToListAsync(ct);
        return ToDomain(record, entries);
    }

    private static ConflictException ConcurrencyConflict() =>
        new("ConcurrencyConflict", "The timesheet was changed by someone else. Reload and try again.");

    private static Timesheet ToDomain(TimesheetRecord record, IEnumerable<TimeEntryRecord> entries) =>
        Timesheet.Restore(
            new TimesheetId(record.Id),
            new UserId(record.OwnerId),
            new Week(record.WeekStart),
            Enum.Parse<TimesheetStatus>(record.Status),
            record.Version,
            entries.Select(ToDomain));

    private static TimeEntry ToDomain(TimeEntryRecord record) =>
        TimeEntry.Restore(
            new TimeEntryId(record.Id),
            record.StartedAt,
            record.EndedAt,
            new PunchSource(record.ClockInIp, record.ClockInUserAgent),
            record.ClockOutIp is null ? null : new PunchSource(record.ClockOutIp, record.ClockOutUserAgent ?? ""),
            record.IsManuallyChanged);

    private static TimesheetRecord ToRecord(Timesheet timesheet, int version) => new()
    {
        Id = timesheet.Id.Value,
        OwnerId = timesheet.OwnerId.Value,
        WeekStart = timesheet.Week.Start,
        Status = timesheet.Status.ToString(),
        Version = version,
    };

    private static TimeEntryRecord ToRecord(TimeEntry entry, Timesheet timesheet) => new()
    {
        Id = entry.Id.Value,
        TimesheetId = timesheet.Id.Value,
        OwnerId = timesheet.OwnerId.Value,
        StartedAt = entry.Start,
        EndedAt = entry.End,
        ClockInIp = entry.ClockInSource.IpAddress,
        ClockInUserAgent = entry.ClockInSource.UserAgent,
        ClockOutIp = entry.ClockOutSource?.IpAddress,
        ClockOutUserAgent = entry.ClockOutSource?.UserAgent,
        IsManuallyChanged = entry.IsManuallyChanged,
    };
}
