using ClockIn.Application.Reporting;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using ClockIn.Persistence.Accounts;
using ClockIn.Persistence.Common;
using ClockIn.Persistence.Teams;
using ClockIn.Persistence.Timesheets;
using LinqToDB;
using LinqToDB.Async;

namespace ClockIn.Persistence.Reporting;

public sealed class ReportingReads(AppDataConnection db) : IReportingReads
{
    private static readonly string[] ManualKinds =
        [nameof(TimesheetEventKind.EntryAdded), nameof(TimesheetEventKind.EntryEdited), nameof(TimesheetEventKind.EntryDeleted)];

    public async Task<IReadOnlyList<PersonRow>> PeopleAsync(CancellationToken ct) =>
        (await db.GetTable<UserRecord>().ToListAsync(ct))
            .ConvertAll(u => new PersonRow(u.Id, u.Name, u.Email, Enum.Parse<Role>(u.Role), Enum.Parse<UserStatus>(u.Status), u.TeamId, u.LockedUntil));

    public async Task<IReadOnlyList<TeamRow>> TeamsAsync(CancellationToken ct) =>
        (await db.GetTable<TeamRecord>().ToListAsync(ct)).ConvertAll(t => new TeamRow(t.Id, t.Name, t.SupervisorId, t.IsArchived));

    public async Task<IReadOnlyList<TimesheetRow>> TimesheetsAsync(IReadOnlyCollection<Guid> ownerIds, DateOnly weekStart, CancellationToken ct)
    {
        if (ownerIds.Count == 0)
        {
            return [];
        }

        Guid[] owners = [.. ownerIds];
        List<TimesheetRecord> timesheets = await db.GetTable<TimesheetRecord>()
            .Where(t => owners.Contains(t.OwnerId) && t.WeekStart == weekStart)
            .ToListAsync(ct);
        Guid[] ids = [.. timesheets.Select(t => t.Id)];
        if (ids.Length == 0)
        {
            return [];
        }

        ILookup<Guid, TimeEntryRecord> entries = (await db.GetTable<TimeEntryRecord>().Where(e => ids.Contains(e.TimesheetId)).ToListAsync(ct))
            .ToLookup(e => e.TimesheetId);
        var submissions = (await db.GetTable<TimesheetHistoryRecord>()
            .Where(h => ids.Contains(h.TimesheetId) && h.Kind == nameof(TimesheetEventKind.Submitted))
            .GroupBy(h => h.TimesheetId).Select(g => new { Id = g.Key, At = g.Max(h => (DateTimeOffset?)h.At) })
            .ToListAsync(ct)).ToDictionary(r => r.Id, r => r.At);
        var manualChanges = (await db.GetTable<TimesheetHistoryRecord>()
            .Where(h => ids.Contains(h.TimesheetId) && ManualKinds.Contains(h.Kind))
            .Select(h => new { h.TimesheetId, h.EntryId }).Distinct().GroupBy(h => h.TimesheetId)
            .Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct)).ToDictionary(r => r.Id, r => r.Count);

        return timesheets.ConvertAll(t => new TimesheetRow(
            t.Id,
            t.OwnerId,
            t.WeekStart,
            Enum.Parse<TimesheetStatus>(t.Status),
            t.ReturnComment,
            t.ApprovedTeamId,
            submissions.GetValueOrDefault(t.Id),
            manualChanges.GetValueOrDefault(t.Id),
            [.. entries[t.Id].OrderBy(e => e.StartedAt).Select(e => new EntryRow(e.Id, e.StartedAt, e.EndedAt, e.IsManuallyChanged, e.Note))]));
    }

    public async Task<ToDosDto> PersonalToDosAsync(Guid ownerId, DateOnly currentWeek, DateTimeOffset declinedSince, CancellationToken ct)
    {
        var drafts = await db.GetTable<TimesheetRecord>()
            .Where(t => t.OwnerId == ownerId && t.Status == nameof(TimesheetStatus.Draft) && (t.ReturnComment != null || t.WeekStart < currentWeek))
            .OrderBy(t => t.WeekStart).Select(t => new { t.WeekStart, t.ReturnComment }).ToListAsync(ct);
        DateTimeOffset? since = await db.GetTable<TimeEntryRecord>()
            .Where(e => e.OwnerId == ownerId && e.EndedAt == null).Select(e => (DateTimeOffset?)e.StartedAt).FirstOrDefaultAsync(ct);
        int declined = await db.GetTable<CorrectionRequestRecord>().CountAsync(r =>
            r.OwnerId == ownerId && r.Status == nameof(CorrectionStatus.Declined) && r.DecidedAt >= declinedSince, ct);
        return new ToDosDto(since,
            [.. drafts.Where(t => t.ReturnComment is not null).Select(t => t.WeekStart)],
            [.. drafts.Where(t => t.ReturnComment is null && t.WeekStart < currentWeek).Select(t => t.WeekStart)], declined, 0, 0);
    }

    public async Task<(int Timesheets, int Corrections)> ReviewCountsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken ct)
    {
        Guid[] owners = [.. ownerIds];
        int sheets = await db.GetTable<TimesheetRecord>().CountAsync(t => owners.Contains(t.OwnerId) && t.Status == nameof(TimesheetStatus.Submitted), ct);
        int corrections = await db.GetTable<CorrectionRequestRecord>().CountAsync(r => owners.Contains(r.OwnerId) && r.Status == nameof(CorrectionStatus.Pending), ct);
        return (sheets, corrections);
    }

    public async Task<bool> HasPendingReviewsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken ct)
    {
        Guid[] owners = [.. ownerIds];
        return await db.GetTable<TimesheetRecord>().AnyAsync(t => owners.Contains(t.OwnerId) && t.Status == nameof(TimesheetStatus.Submitted), ct)
            || await db.GetTable<CorrectionRequestRecord>().AnyAsync(r => owners.Contains(r.OwnerId) && r.Status == nameof(CorrectionStatus.Pending), ct);
    }

    public async Task<IReadOnlyList<ClockedInDto>> ClockedInAsync(IReadOnlyCollection<Guid> ownerIds, DateTimeOffset now, CancellationToken ct)
    {
        Guid[] owners = [.. ownerIds];
        var rows = await (
            from entry in db.GetTable<TimeEntryRecord>()
            join person in db.GetTable<UserRecord>() on entry.OwnerId equals person.Id
            from team in db.GetTable<TeamRecord>().Where(t => t.Id == person.TeamId).DefaultIfEmpty()
            where owners.Contains(entry.OwnerId) && entry.EndedAt == null
            orderby entry.StartedAt, entry.Id
            select new { entry.OwnerId, person.Name, TeamName = team.Name, entry.StartedAt }).ToListAsync(ct);
        return rows.ConvertAll(r => new ClockedInDto(r.OwnerId, r.Name, r.TeamName, r.StartedAt,
            (long)(now - r.StartedAt).TotalSeconds, now - r.StartedAt > TimeEntry.MaxLength));
    }

    public async Task<ReviewQueueDto> ReviewQueueAsync(
        IReadOnlyCollection<Guid> ownerIds, int timesheetPage, int correctionPage, DateTimeOffset now, CancellationToken ct)
    {
        Guid[] owners = [.. ownerIds];
        var submitted = db.GetTable<TimesheetRecord>()
            .Where(t => owners.Contains(t.OwnerId) && t.Status == nameof(TimesheetStatus.Submitted))
            .Select(t => new
            {
                Sheet = t,
                SubmittedAt = db.GetTable<TimesheetHistoryRecord>()
                    .Where(h => h.TimesheetId == t.Id && h.Kind == nameof(TimesheetEventKind.Submitted)).Max(h => (DateTimeOffset?)h.At),
            })
            .OrderBy(t => t.Sheet.WeekStart).ThenBy(t => t.SubmittedAt).ThenBy(t => t.Sheet.Id)
            .Skip((timesheetPage - 1) * ReportPageConstants.Size).Take(ReportPageConstants.Size + 1).AsSubQuery();
        List<QueuedTimesheetDto> sheets = await (
            from row in submitted
            join person in db.GetTable<UserRecord>() on row.Sheet.OwnerId equals person.Id
            from team in db.GetTable<TeamRecord>().Where(t => t.Id == person.TeamId).DefaultIfEmpty()
            orderby row.Sheet.WeekStart, row.SubmittedAt, row.Sheet.Id
            select new QueuedTimesheetDto(row.Sheet.Id, row.Sheet.OwnerId, person.Name, team.Name, row.Sheet.WeekStart,
                db.GetTable<TimeEntryRecord>().Where(e => e.TimesheetId == row.Sheet.Id)
                    .Sum(e => (long?)DurationSeconds(e.StartedAt, e.EndedAt ?? now)) ?? 0, row.SubmittedAt)).ToListAsync(ct);

        var pending = await (
            from request in db.GetTable<CorrectionRequestRecord>()
            join sheet in db.GetTable<TimesheetRecord>() on request.TimesheetId equals sheet.Id
            join person in db.GetTable<UserRecord>() on request.OwnerId equals person.Id
            from team in db.GetTable<TeamRecord>().Where(t => t.Id == person.TeamId).DefaultIfEmpty()
            where owners.Contains(request.OwnerId) && request.Status == nameof(CorrectionStatus.Pending)
            orderby request.RequestedAt, request.Id
            select new { Request = request, Sheet = sheet, person.Name, TeamName = team.Name })
            .Skip((correctionPage - 1) * ReportPageConstants.Size).Take(ReportPageConstants.Size + 1).ToListAsync(ct);
        return new ReviewQueueDto(sheets.Take(ReportPageConstants.Size).ToArray(),
            pending.Take(ReportPageConstants.Size).Select(r => new QueuedCorrectionDto(
                r.Request.Id, r.Sheet.Id, r.Request.OwnerId, r.Name, r.TeamName, r.Sheet.WeekStart,
                Enum.Parse<WeekStatus>(r.Sheet.Status), r.Request.Reason, r.Request.RequestedAt)).ToArray(),
            sheets.Count > ReportPageConstants.Size, pending.Count > ReportPageConstants.Size);
    }

    public async Task<ReportPageDto<ExceptionDto>> ExceptionsAsync(
        IReadOnlyCollection<Guid> ownerIds, DateOnly currentWeek, int page, CancellationToken ct)
    {
        Guid[] owners = [.. ownerIds];
        var eligible = from sheet in db.GetTable<TimesheetRecord>()
                       join person in db.GetTable<UserRecord>() on sheet.OwnerId equals person.Id
                       from team in db.GetTable<TeamRecord>().Where(t => t.Id == person.TeamId).DefaultIfEmpty()
                       where owners.Contains(sheet.OwnerId) && sheet.Status != nameof(TimesheetStatus.Approved)
                       select new { Sheet = sheet, person.Name, TeamName = team.Name };
        var overdue = eligible.Where(r => r.Sheet.Status == nameof(TimesheetStatus.Draft) && r.Sheet.WeekStart < currentWeek)
            .Select(r => new ExceptionDto(ExceptionKind.OverdueDraft, r.Sheet.OwnerId, r.Name, r.TeamName, r.Sheet.WeekStart, r.Sheet.Id, null, null, null));
        var longEntries = from row in eligible
                          join entry in db.GetTable<TimeEntryRecord>() on row.Sheet.Id equals entry.TimesheetId
                          where entry.EndedAt != null && entry.EndedAt > entry.StartedAt.AddHours(WorkedTimeHelper.LongEntry.TotalHours)
                          select new ExceptionDto(ExceptionKind.LongEntry, row.Sheet.OwnerId, row.Name, row.TeamName,
                              row.Sheet.WeekStart, row.Sheet.Id, entry.Id, DurationSeconds(entry.StartedAt, entry.EndedAt!.Value), null);
        var manual = eligible.Select(r => new
        {
            Row = r,
            Count = db.GetTable<TimesheetHistoryRecord>()
                .Where(h => h.TimesheetId == r.Sheet.Id && ManualKinds.Contains(h.Kind)).Select(h => h.EntryId).Distinct().Count(),
        }).Where(r => r.Count >= WorkedTimeHelper.ManyManualChanges)
            .Select(r => new ExceptionDto(ExceptionKind.ManyManualChanges, r.Row.Sheet.OwnerId, r.Row.Name, r.Row.TeamName,
                r.Row.Sheet.WeekStart, r.Row.Sheet.Id, null, null, r.Count));
        List<ExceptionDto> items = await overdue.Concat(longEntries).Concat(manual)
            .OrderByDescending(e => e.WeekStart).ThenBy(e => Sql.Lower(e.Name)).ThenBy(e => e.Kind)
            .ThenBy(e => e.TimesheetId).ThenBy(e => e.EntryId)
            .Skip((page - 1) * ReportPageConstants.Size).Take(ReportPageConstants.Size + 1).ToListAsync(ct);
        return new ReportPageDto<ExceptionDto>(items.Take(ReportPageConstants.Size).ToArray(), items.Count > ReportPageConstants.Size);
    }

    [Sql.Expression(ProviderName.PostgreSQL, "CAST(TRUNC(EXTRACT(EPOCH FROM ({1} - {0}))) AS bigint)", ServerSideOnly = true)]
    [Sql.Expression(ProviderName.SQLite, "CAST(ROUND((julianday({1}) - julianday({0})) * 86400000) / 1000 AS INTEGER)", ServerSideOnly = true)]
    private static long DurationSeconds(DateTimeOffset start, DateTimeOffset end) => (long)(end - start).TotalSeconds;
}
