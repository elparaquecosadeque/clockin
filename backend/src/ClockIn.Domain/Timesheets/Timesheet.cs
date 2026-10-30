using Ardalis.GuardClauses;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Common.Guards;

namespace ClockIn.Domain.Timesheets;

/// <summary>One user's week: its entries and its approval state.</summary>
public sealed class Timesheet
{
    private readonly List<TimeEntry> _entries = [];

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

    /// <summary>0 until first saved; the repository checks and bumps it on every save.</summary>
    public int Version { get; private init; }

    public IReadOnlyList<TimeEntry> Entries => _entries;

    public TimeEntry? OpenEntry => _entries.SingleOrDefault(e => e.End is null);

    public static Timesheet Start(UserId ownerId, Week week) => new(TimesheetId.New(), ownerId, week);

    /// <summary>Rebuilds a saved timesheet. For repositories only; it checks no rules.</summary>
    public static Timesheet Restore(TimesheetId id, UserId ownerId, Week week, TimesheetStatus status, int version, IEnumerable<TimeEntry> entries)
    {
        var timesheet = new Timesheet(id, ownerId, week) { Status = status, Version = version };
        timesheet._entries.AddRange(entries);
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

    private void EnsureEditable()
    {
        if (Status != TimesheetStatus.Draft)
        {
            throw new DomainException("TimesheetNotEditable", "Only a Draft timesheet accepts changes.");
        }
    }
}
