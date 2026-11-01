using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;
using ClockIn.Domain.Timesheets;
using ClockIn.Persistence.Common;
using LinqToDB;
using LinqToDB.Async;

namespace ClockIn.Persistence.Timesheets;

/// <remarks>Saves run inside the command's transaction (TransactionMiddleware).</remarks>
public sealed class TimesheetRepository(AppDataConnection db) : ITimesheetRepository
{
    public async Task<Timesheet?> FindAsync(TimesheetId id, CancellationToken ct)
    {
        TimesheetRecord? record = await db.GetTable<TimesheetRecord>().FirstOrDefaultAsync(t => t.Id == id.Value, ct);
        return record is null ? null : await LoadAsync(record, ct);
    }

    public async Task<Timesheet?> FindAsync(UserId ownerId, Week week, CancellationToken ct)
    {
        TimesheetRecord? record = await db.GetTable<TimesheetRecord>()
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId.Value && t.WeekStart == week.Start, ct);
        return record is null ? null : await LoadAsync(record, ct);
    }

    public async Task<Timesheet?> FindByEntryAsync(TimeEntryId entryId, CancellationToken ct)
    {
        TimesheetRecord? record = await (
            from entry in db.GetTable<TimeEntryRecord>()
            join timesheet in db.GetTable<TimesheetRecord>() on entry.TimesheetId equals timesheet.Id
            where entry.Id == entryId.Value
            select timesheet).FirstOrDefaultAsync(ct);
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

    public async Task SaveOrUpdateAsync(Timesheet timesheet, CancellationToken ct)
    {
        await SaveTimesheetAsync(timesheet, ct);
        await SaveEntriesAsync(timesheet, ct);
        await AppendHistoryAsync(timesheet, ct);
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
                .Set(t => t.ApprovedBy, timesheet.Approval == null ? null : timesheet.Approval.ApproverId.Value)
                .Set(t => t.ApprovedAt, timesheet.Approval == null ? null : timesheet.Approval.At)
                .Set(t => t.ApprovedTeamId, timesheet.Approval == null || timesheet.Approval.TeamId == null ? null : timesheet.Approval.TeamId.Value.Value)
                .Set(t => t.ReturnComment, timesheet.ReturnComment)
                .Set(t => t.Version, timesheet.Version + 1)
                .UpdateAsync(ct);
            if (updated == 0)
            {
                throw ConcurrencyConflict();
            }
        }
        catch (Exception ex) when (UniqueViolationHelper.Is(ex))
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
        catch (Exception ex) when (UniqueViolationHelper.Is(ex))
        {
            // The only unique index on entries besides the key: an open entry already exists, possibly in another week.
            throw new ConflictException("EntryAlreadyOpen", "An entry is already open. Clock out first.");
        }
    }

    private async Task AppendHistoryAsync(Timesheet timesheet, CancellationToken ct)
    {
        int position = timesheet.History.Count - timesheet.NewHistory.Count();
        foreach (TimesheetEvent item in timesheet.NewHistory)
        {
            await db.InsertAsync(ToRecord(item, timesheet.Id, position++), token: ct);
        }
    }

    private async Task<Timesheet> LoadAsync(TimesheetRecord record, CancellationToken ct)
    {
        List<TimeEntryRecord> entries = await db.GetTable<TimeEntryRecord>()
            .Where(e => e.TimesheetId == record.Id)
            .OrderBy(e => e.StartedAt)
            .ToListAsync(ct);
        List<TimesheetHistoryRecord> history = await db.GetTable<TimesheetHistoryRecord>()
            .Where(h => h.TimesheetId == record.Id)
            .OrderBy(h => h.Position)
            .ToListAsync(ct);
        return ToDomain(record, entries, history);
    }

    private static ConflictException ConcurrencyConflict() =>
        new("ConcurrencyConflict", "The timesheet was changed by someone else. Reload and try again.");

    private static Timesheet ToDomain(TimesheetRecord record, IEnumerable<TimeEntryRecord> entries, IEnumerable<TimesheetHistoryRecord> history) =>
        Timesheet.Restore(
            new TimesheetId(record.Id),
            new UserId(record.OwnerId),
            new Week(record.WeekStart),
            Enum.Parse<TimesheetStatus>(record.Status),
            record.Version,
            record.ApprovedBy is { } approver
                ? new Approval(new UserId(approver), record.ApprovedAt!.Value, record.ApprovedTeamId is { } team ? new TeamId(team) : null)
                : null,
            record.ReturnComment,
            entries.Select(ToDomain),
            history.Select(ToDomain));

    private static TimeEntry ToDomain(TimeEntryRecord record) =>
        TimeEntry.Restore(
            new TimeEntryId(record.Id),
            record.StartedAt,
            record.EndedAt,
            new PunchSource(record.ClockInIp, record.ClockInUserAgent),
            record.ClockOutIp is null ? null : new PunchSource(record.ClockOutIp, record.ClockOutUserAgent ?? ""),
            Enum.Parse<ManualChange>(record.ManualChange),
            record.ChangedBy is { } by ? new UserId(by) : null,
            record.Note,
            record.PunchedStart,
            record.PunchedEnd);

    private static TimesheetEvent ToDomain(TimesheetHistoryRecord record)
    {
        EntrySnapshot? before = record.BeforeStart is { } bs
            ? new EntrySnapshot(new TimeEntryId(record.EntryId!.Value), bs, record.BeforeEnd, record.BeforePunchedStart, record.BeforePunchedEnd)
            : null;
        EntrySnapshot? after = record.AfterStart is { } aS
            ? new EntrySnapshot(new TimeEntryId(record.EntryId!.Value), aS, record.AfterEnd, record.AfterPunchedStart, record.AfterPunchedEnd)
            : null;
        return new TimesheetEvent(
            Enum.Parse<TimesheetEventKind>(record.Kind), new UserId(record.ActorId), record.At, record.Comment, before, after);
    }

    private static TimesheetRecord ToRecord(Timesheet timesheet, int version) => new()
    {
        Id = timesheet.Id.Value,
        OwnerId = timesheet.OwnerId.Value,
        WeekStart = timesheet.Week.Start,
        Status = timesheet.Status.ToString(),
        Version = version,
        ApprovedBy = timesheet.Approval?.ApproverId.Value,
        ApprovedAt = timesheet.Approval?.At,
        ApprovedTeamId = timesheet.Approval?.TeamId?.Value,
        ReturnComment = timesheet.ReturnComment,
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
        ManualChange = entry.ManualChange.ToString(),
        ChangedBy = entry.ChangedBy?.Value,
        Note = entry.Note,
        PunchedStart = entry.PunchedStart,
        PunchedEnd = entry.PunchedEnd,
    };

    private static TimesheetHistoryRecord ToRecord(TimesheetEvent item, TimesheetId timesheetId, int position) => new()
    {
        TimesheetId = timesheetId.Value,
        Position = position,
        Kind = item.Kind.ToString(),
        ActorId = item.ActorId.Value,
        At = item.At,
        Comment = item.Comment,
        EntryId = (item.After ?? item.Before)?.EntryId.Value,
        BeforeStart = item.Before?.Start,
        BeforeEnd = item.Before?.End,
        BeforePunchedStart = item.Before?.PunchedStart,
        BeforePunchedEnd = item.Before?.PunchedEnd,
        AfterStart = item.After?.Start,
        AfterEnd = item.After?.End,
        AfterPunchedStart = item.After?.PunchedStart,
        AfterPunchedEnd = item.After?.PunchedEnd,
    };
}
