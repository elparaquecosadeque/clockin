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

    public IReadOnlyList<TimeEntry> Entries => _entries;

    public TimeEntry? OpenEntry => _entries.SingleOrDefault(e => e.End is null);

    public static Timesheet Start(UserId ownerId, Week week) => new(TimesheetId.New(), ownerId, week);

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
