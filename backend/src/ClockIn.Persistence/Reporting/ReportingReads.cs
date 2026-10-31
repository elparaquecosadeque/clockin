using ClockIn.Application.Reporting.Interfaces;
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

    public async Task<IReadOnlyList<TimesheetRow>> TimesheetsAsync(IReadOnlyCollection<Guid> ownerIds, DateOnly? weekStart, CancellationToken ct)
    {
        if (ownerIds.Count == 0)
        {
            return [];
        }

        Guid[] owners = [.. ownerIds];
        List<TimesheetRecord> timesheets = await db.GetTable<TimesheetRecord>()
            .Where(t => owners.Contains(t.OwnerId) && (weekStart == null || t.WeekStart == weekStart))
            .ToListAsync(ct);
        Guid[] ids = [.. timesheets.Select(t => t.Id)];
        if (ids.Length == 0)
        {
            return [];
        }

        ILookup<Guid, TimeEntryRecord> entries = (await db.GetTable<TimeEntryRecord>().Where(e => ids.Contains(e.TimesheetId)).ToListAsync(ct))
            .ToLookup(e => e.TimesheetId);
        ILookup<Guid, TimesheetHistoryRecord> history = (await db.GetTable<TimesheetHistoryRecord>()
            .Where(h => ids.Contains(h.TimesheetId) && (ManualKinds.Contains(h.Kind) || h.Kind == nameof(TimesheetEventKind.Submitted)))
            .ToListAsync(ct)).ToLookup(h => h.TimesheetId);

        return timesheets.ConvertAll(t => new TimesheetRow(
            t.Id,
            t.OwnerId,
            t.WeekStart,
            Enum.Parse<TimesheetStatus>(t.Status),
            t.ReturnComment,
            t.ApprovedTeamId,
            history[t.Id].Where(h => h.Kind == nameof(TimesheetEventKind.Submitted)).Select(h => (DateTimeOffset?)h.At).Max(),
            history[t.Id].Where(h => ManualKinds.Contains(h.Kind)).Select(h => h.EntryId).Distinct().Count(),
            [.. entries[t.Id].OrderBy(e => e.StartedAt).Select(e => new EntryRow(e.Id, e.StartedAt, e.EndedAt, e.IsManuallyChanged, e.Note))]));
    }

    public async Task<IReadOnlyList<CorrectionRow>> CorrectionsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken ct)
    {
        if (ownerIds.Count == 0)
        {
            return [];
        }

        Guid[] owners = [.. ownerIds];
        var rows = await (
            from request in db.GetTable<CorrectionRequestRecord>()
            join timesheet in db.GetTable<TimesheetRecord>() on request.TimesheetId equals timesheet.Id
            where owners.Contains(request.OwnerId)
            select new { Request = request, timesheet.WeekStart }).ToListAsync(ct);
        return rows.ConvertAll(r => new CorrectionRow(
            r.Request.Id,
            r.Request.TimesheetId,
            r.Request.OwnerId,
            r.WeekStart,
            Enum.Parse<CorrectionStatus>(r.Request.Status),
            r.Request.Reason,
            r.Request.RequestedAt,
            r.Request.DecidedAt));
    }
}
