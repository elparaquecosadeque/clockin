using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Reporting;

/// <summary>
/// The reporting specs' company behind a substituted reads port: Sam leads Warehouse (Eve, Ed), Sue leads Office
/// (Olga), Ada is an admin. It is Friday 2026-10-30 10:00.
/// </summary>
public sealed class ReportingWorld
{
    public static readonly DateOnly LastWeek = new(2026, 10, 19);
    public static readonly DateOnly ThisWeek = new(2026, 10, 26);

    private readonly List<PersonRow> _people = [];
    private readonly List<TimesheetRow> _timesheets = [];
    private readonly List<CorrectionRow> _corrections = [];

    public ReportingWorld()
    {
        Ada = Person("Ada", Role.Admin, null);
        Sam = Person("Sam", Role.Supervisor, null);
        Sue = Person("Sue", Role.Supervisor, null);
        Warehouse = new TeamRow(Guid.CreateVersion7(), "Warehouse", Sam.Id, false);
        Office = new TeamRow(Guid.CreateVersion7(), "Office", Sue.Id, false);
        Eve = Person("Eve", Role.Employee, Warehouse.Id);
        Ed = Person("Ed", Role.Employee, Warehouse.Id);
        Olga = Person("Olga", Role.Employee, Office.Id);

        Reads.PeopleAsync(Arg.Any<CancellationToken>()).Returns(_ => _people);
        Reads.TeamsAsync(Arg.Any<CancellationToken>()).Returns(_ => (IReadOnlyList<TeamRow>)[Warehouse, Office]);
        Reads.TimesheetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyList<TimesheetRow>)[.. _timesheets.Where(t =>
                c.Arg<IReadOnlyCollection<Guid>>().Contains(t.OwnerId) && t.WeekStart == c.Arg<DateOnly>())]);
        Reads.ReviewCountsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(c => (_timesheets.Count(t => c.Arg<IReadOnlyCollection<Guid>>().Contains(t.OwnerId) && t.Status == TimesheetStatus.Submitted),
                _corrections.Count(r => c.Arg<IReadOnlyCollection<Guid>>().Contains(r.OwnerId) && r.Status == CorrectionStatus.Pending)));
        Reads.PersonalToDosAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                Guid owner = c.Arg<Guid>();
                List<TimesheetRow> mine = [.. _timesheets.Where(t => t.OwnerId == owner).OrderBy(t => t.WeekStart)];
                return new ToDosDto(mine.SelectMany(t => t.Entries).FirstOrDefault(e => e.End is null)?.Start,
                    [.. mine.Where(t => t.Status == TimesheetStatus.Draft && t.ReturnComment is not null).Select(t => t.WeekStart)],
                    [.. mine.Where(t => t.Status == TimesheetStatus.Draft && t.ReturnComment is null && t.WeekStart < c.Arg<DateOnly>()).Select(t => t.WeekStart)],
                    _corrections.Count(r => r.OwnerId == owner && r.Status == CorrectionStatus.Declined && r.DecidedAt >= c.Arg<DateTimeOffset>()), 0, 0);
            });
        Reads.ClockedInAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyList<ClockedInDto>)[.. _timesheets.Where(t => c.Arg<IReadOnlyCollection<Guid>>().Contains(t.OwnerId))
                .SelectMany(t => t.Entries.Where(e => e.End is null).Select(e => new { t.OwnerId, Entry = e }))
                .OrderBy(r => r.Entry.Start).Select(r => new ClockedInDto(r.OwnerId, _people.Single(p => p.Id == r.OwnerId).Name,
                    new[] { Warehouse, Office }.FirstOrDefault(t => t.Id == _people.Single(p => p.Id == r.OwnerId).TeamId)?.Name,
                    r.Entry.Start, WorkedTimeHelper.Seconds(r.Entry, c.Arg<DateTimeOffset>()), c.Arg<DateTimeOffset>() - r.Entry.Start > TimeEntry.MaxLength))]);
        Reads.ExceptionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(c => new ReportPageDto<ExceptionDto>(Exceptions(c.Arg<IReadOnlyCollection<Guid>>(), c.Arg<DateOnly>()), false));
        SignIn(Sam);
    }

    private IReadOnlyList<ExceptionDto> Exceptions(IReadOnlyCollection<Guid> owners, DateOnly currentWeek)
    {
        List<ExceptionDto> found = [];
        foreach (TimesheetRow sheet in _timesheets.Where(t => owners.Contains(t.OwnerId) && t.Status != TimesheetStatus.Approved))
        {
            PersonRow owner = _people.Single(p => p.Id == sheet.OwnerId);
            string? team = new[] { Warehouse, Office }.FirstOrDefault(t => t.Id == owner.TeamId)?.Name;
            if (sheet.Status == TimesheetStatus.Draft && sheet.WeekStart < currentWeek)
            {
                found.Add(new(ExceptionKind.OverdueDraft, owner.Id, owner.Name, team, sheet.WeekStart, sheet.Id, null, null, null));
            }
            found.AddRange(sheet.Entries.Where(e => e.End - e.Start > WorkedTimeHelper.LongEntry)
                .Select(e => new ExceptionDto(ExceptionKind.LongEntry, owner.Id, owner.Name, team, sheet.WeekStart, sheet.Id, e.Id, WorkedTimeHelper.Seconds(e, Clock.GetUtcNow()), null)));
            if (sheet.ManuallyChangedEntries >= WorkedTimeHelper.ManyManualChanges)
            {
                found.Add(new(ExceptionKind.ManyManualChanges, owner.Id, owner.Name, team, sheet.WeekStart, sheet.Id, null, null, sheet.ManuallyChangedEntries));
            }
        }
        return [.. found.OrderByDescending(e => e.WeekStart).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Kind)];
    }

    public PersonRow Ada { get; }

    public PersonRow Sam { get; }

    public PersonRow Sue { get; }

    public PersonRow Eve { get; private set; }

    public PersonRow Ed { get; private set; }

    public PersonRow Olga { get; }

    public TeamRow Warehouse { get; }

    public TeamRow Office { get; }

    public CompanyClock Clock { get; } = new("2026-10-30 10:00");

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public IReportingReads Reads { get; } = Substitute.For<IReportingReads>();

    public void SignIn(PersonRow person) => CurrentUser.Id.Returns(new UserId(person.Id));

    public PersonRow Person(string name, Role role, Guid? teamId, UserStatus status = UserStatus.Active)
    {
        var person = new PersonRow(Guid.CreateVersion7(), name, $"{name.ToLowerInvariant()}@clockin.test", role, status, teamId);
        _people.Add(person);
        return person;
    }

    public void Move(PersonRow person, Guid teamId)
    {
        _people[_people.IndexOf(person)] = person with { TeamId = teamId };
        if (person == Eve)
        {
            Eve = person with { TeamId = teamId };
        }
    }

    /// <summary>A timesheet with entries given as company-time spans ("2026-10-26 08:00", "2026-10-26 16:00"); a null end is open.</summary>
    public TimesheetRow Timesheet(
        PersonRow owner, DateOnly week, TimesheetStatus status = TimesheetStatus.Draft, (string Start, string? End)[]? entries = null,
        string? returnComment = null, Guid? approvedTeam = null, int manualChanges = 0)
    {
        var row = new TimesheetRow(
            Guid.CreateVersion7(), owner.Id, week, status, returnComment, approvedTeam,
            status == TimesheetStatus.Draft ? null : CompanyClock.At($"{week.AddDays(7):yyyy-MM-dd} 09:00"),
            manualChanges,
            [.. (entries ?? []).Select(e => new EntryRow(
                Guid.CreateVersion7(), CompanyClock.At(e.Start), e.End is null ? null : CompanyClock.At(e.End), false, null))]);
        _timesheets.Add(row);
        return row;
    }

    public void Add(TimesheetRow row) => _timesheets.Add(row);

    public CorrectionRow Correction(TimesheetRow timesheet, CorrectionStatus status = CorrectionStatus.Pending, string? decidedAt = null)
    {
        var row = new CorrectionRow(
            Guid.CreateVersion7(), timesheet.Id, timesheet.OwnerId, timesheet.WeekStart, status, "missing shift",
            CompanyClock.At("2026-10-20 09:00"), decidedAt is null ? null : CompanyClock.At(decidedAt));
        _corrections.Add(row);
        return row;
    }
}
