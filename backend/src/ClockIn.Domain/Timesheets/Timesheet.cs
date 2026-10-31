using Ardalis.GuardClauses;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Common.Guards;
using ClockIn.Domain.Teams;

namespace ClockIn.Domain.Timesheets;

/// <summary>One user's week: its entries, its approval state and its history.</summary>
public sealed class Timesheet
{
    public const int MaxCommentLength = 1000;

    private readonly List<TimeEntry> _entries = [];
    private readonly List<TimesheetEvent> _history = [];
    private int _savedHistoryCount;

    private Timesheet(TimesheetId id, UserId ownerId, Week week)
    {
        Id = id;
        OwnerId = ownerId;
        Week = week;
    }

    public TimesheetId Id { get; }

    public UserId OwnerId { get; }

    public Week Week { get; }

    public TimesheetStatus Status { get; private set; } = TimesheetStatus.Draft;

    /// <summary>Set while Approved; history keeps earlier approvals.</summary>
    public Approval? Approval { get; private set; }

    /// <summary>What the approver asked to fix, until the owner submits again.</summary>
    public string? ReturnComment { get; private set; }

    /// <summary>0 until first saved; the repository checks and bumps it on every save.</summary>
    public int Version { get; private init; }

    public IReadOnlyList<TimeEntry> Entries => _entries;

    public TimeEntry? OpenEntry => _entries.SingleOrDefault(e => e.End is null);

    /// <summary>Oldest first.</summary>
    public IReadOnlyList<TimesheetEvent> History => _history;

    /// <summary>History added since the timesheet was loaded, for the repository to append.</summary>
    public IEnumerable<TimesheetEvent> NewHistory => _history.Skip(_savedHistoryCount);

    /// <summary>Entries added, edited or deleted by hand, each counted once.</summary>
    public int ManuallyChangedEntryCount => _history
        .Where(e => e.Kind is TimesheetEventKind.EntryAdded or TimesheetEventKind.EntryEdited or TimesheetEventKind.EntryDeleted)
        .Select(e => (e.After ?? e.Before)!.EntryId)
        .Distinct()
        .Count();

    public static Timesheet Start(UserId ownerId, Week week) => new(TimesheetId.New(), ownerId, week);

    /// <summary>Rebuilds a saved timesheet. For repositories only; it checks no rules.</summary>
    public static Timesheet Restore(
        TimesheetId id, UserId ownerId, Week week, TimesheetStatus status, int version, Approval? approval, string? returnComment,
        IEnumerable<TimeEntry> entries, IEnumerable<TimesheetEvent> history)
    {
        var timesheet = new Timesheet(id, ownerId, week)
        {
            Status = status,
            Version = version,
            Approval = approval,
            ReturnComment = returnComment,
        };
        timesheet._entries.AddRange(entries);
        timesheet._history.AddRange(history);
        timesheet._savedHistoryCount = timesheet._history.Count;
        return timesheet;
    }

    /// <remarks>
    /// Only checks entries of this week. The handler checks other weeks, and a database
    /// constraint guarantees one open entry per user under concurrency.
    /// </remarks>
    public void ClockIn(DateTimeOffset now, PunchSource source, TimeZoneInfo companyZone)
    {
        Guard.Against.OutsideWeek(now, Week, companyZone);
        EnsureEditable();
        if (OpenEntry is not null)
        {
            throw new DomainException("EntryAlreadyOpen", "An entry is already open. Clock out first.");
        }

        _entries.Add(new TimeEntry(TimeEntryId.New(), now, source));
    }

    public void ClockOut(DateTimeOffset now, PunchSource source)
    {
        TimeEntry entry = OpenEntry ?? throw new DomainException("NoOpenEntry", "There is no open entry to clock out of.");
        entry.ClockOut(now, source);
    }

    /// <summary>A forgotten entry, flagged as added by <paramref name="actor"/>.</summary>
    /// <remarks>
    /// Neighbours are the owner's other timesheets an entry could overlap: the weeks around this one and the one
    /// with the open entry.
    /// </remarks>
    public TimeEntry AddEntry(
        UserId actor, DateTimeOffset start, DateTimeOffset end, string note, PunchSource source, DateTimeOffset now,
        TimeZoneInfo companyZone, IEnumerable<Timesheet> neighbours)
    {
        Guard.Against.OutsideWeek(start, Week, companyZone);
        EnsureEditable();
        CheckSpan(start, end, now, null, neighbours);

        var entry = TimeEntry.Manual(actor, start, end, note.Trim(), source);
        _entries.Add(entry);
        Record(TimesheetEventKind.EntryAdded, actor, now, entry.Note, null, entry.Snapshot);
        return entry;
    }

    /// <summary>A null <paramref name="end"/> keeps an open entry open; a closed entry can't be reopened.</summary>
    /// <remarks>Neighbours as for <see cref="AddEntry"/>.</remarks>
    public void EditEntry(
        UserId actor, TimeEntryId entryId, DateTimeOffset start, DateTimeOffset? end, string note, DateTimeOffset now,
        TimeZoneInfo companyZone, IEnumerable<Timesheet> neighbours)
    {
        EnsureEditable();
        TimeEntry entry = Entry(entryId);
        if (!Week.Contains(start, companyZone))
        {
            throw new DomainException("EntryCannotChangeWeek", "An entry stays in the week it started in.");
        }

        if (end is null && entry.End is not null)
        {
            throw new DomainException("EntryHasNoLength", "An entry must end after it starts.");
        }

        if (end is null && start > now)
        {
            throw new DomainException("EntryInFuture", "An entry can't lie in the future.");
        }

        CheckSpan(start, end ?? now, now, entry, neighbours, open: end is null);

        EntrySnapshot before = entry.Snapshot;
        entry.Change(actor, start, end, note.Trim());
        Record(TimesheetEventKind.EntryEdited, actor, now, entry.Note, before, entry.Snapshot);
    }

    public void DeleteEntry(UserId actor, TimeEntryId entryId, string note, DateTimeOffset now)
    {
        EnsureEditable();
        TimeEntry entry = Entry(entryId);
        _entries.Remove(entry);
        Record(TimesheetEventKind.EntryDeleted, actor, now, note.Trim(), entry.Snapshot, null);
    }

    public void Submit(UserId actor, DateTimeOffset now, TimeZoneInfo companyZone)
    {
        EnsureEditable();
        if (now < Week.EndsAt(companyZone))
        {
            throw new DomainException("WeekNotEnded", "A week can be submitted once it has ended.");
        }

        if (OpenEntry is not null)
        {
            throw new DomainException("OpenEntryInTimesheet", "Clock out or set the open entry's end before submitting.");
        }

        Status = TimesheetStatus.Submitted;
        ReturnComment = null;
        Record(TimesheetEventKind.Submitted, actor, now);
    }

    /// <summary>Keeps the owner's team at this moment, for exports, even if the owner moves later.</summary>
    public void Approve(UserId approver, TeamId? ownerTeam, DateTimeOffset now)
    {
        EnsureSubmitted();
        Status = TimesheetStatus.Approved;
        Approval = new Approval(approver, now, ownerTeam);
        Record(TimesheetEventKind.Approved, approver, now);
    }

    public void Return(UserId approver, string comment, DateTimeOffset now)
    {
        EnsureSubmitted();
        Status = TimesheetStatus.Draft;
        ReturnComment = comment.Trim();
        Record(TimesheetEventKind.Returned, approver, now, ReturnComment);
    }

    /// <summary>An admin's direct reopen.</summary>
    public void Reopen(UserId admin, string reason, DateTimeOffset now)
    {
        if (Status != TimesheetStatus.Approved)
        {
            throw new DomainException("TimesheetNotApproved", "Only an Approved timesheet can be reopened.");
        }

        BackToDraft(admin, reason.Trim(), now);
    }

    /// <summary>The effect of an accepted correction request, in the same transaction.</summary>
    public void ReopenForCorrection(UserId approver, string? comment, DateTimeOffset now)
    {
        EnsureLocked();
        BackToDraft(approver, comment?.Trim(), now);
    }

    public void RecordCorrectionRequested(UserId owner, string reason, DateTimeOffset now)
    {
        EnsureLocked();
        Record(TimesheetEventKind.CorrectionRequested, owner, now, reason.Trim());
    }

    public void RecordCorrectionDeclined(UserId approver, string comment, DateTimeOffset now) =>
        Record(TimesheetEventKind.CorrectionDeclined, approver, now, comment.Trim());

    private void BackToDraft(UserId actor, string? comment, DateTimeOffset now)
    {
        Status = TimesheetStatus.Draft;
        Approval = null;
        Record(TimesheetEventKind.Reopened, actor, now, comment);
    }

    private void CheckSpan(
        DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, TimeEntry? self, IEnumerable<Timesheet> neighbours, bool open = false)
    {
        if (end > now)
        {
            throw new DomainException("EntryInFuture", "An entry can't lie in the future.");
        }

        if (!open)
        {
            TimeEntry.CheckLength(start, end);
        }

        IEnumerable<TimeEntry> others = neighbours
            .Where(t => t.Id != Id)
            .SelectMany(t => t.Entries)
            .Concat(_entries)
            .Where(e => e != self);
        if (others.Any(e => start < (e.End ?? now) && e.Start < end))
        {
            throw new DomainException("EntriesOverlap", "Entries can't overlap.");
        }
    }

    private TimeEntry Entry(TimeEntryId id) =>
        _entries.SingleOrDefault(e => e.Id == id) ?? throw new ArgumentException($"Entry {id.Value} is not in this timesheet.", nameof(id));

    private void Record(
        TimesheetEventKind kind, UserId actor, DateTimeOffset at, string? comment = null, EntrySnapshot? before = null, EntrySnapshot? after = null) =>
        _history.Add(new TimesheetEvent(kind, actor, at, comment, before, after));

    private void EnsureEditable()
    {
        if (Status != TimesheetStatus.Draft)
        {
            throw new DomainException("TimesheetNotEditable", "Only a Draft timesheet accepts changes.");
        }
    }

    private void EnsureSubmitted()
    {
        if (Status != TimesheetStatus.Submitted)
        {
            throw new DomainException("TimesheetNotSubmitted", "Only a Submitted timesheet can be approved or returned.");
        }
    }

    private void EnsureLocked()
    {
        if (Status == TimesheetStatus.Draft)
        {
            throw new DomainException("TimesheetIsEditable", "A Draft can simply be edited.");
        }
    }
}
