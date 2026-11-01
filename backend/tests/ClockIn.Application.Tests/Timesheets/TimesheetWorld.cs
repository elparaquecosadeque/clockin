using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Timesheets;

/// <summary>
/// The people of the specs behind substituted ports: Sam leads Warehouse (Eve, Ed), Sue leads Office (Olga),
/// Ada and Abe are admins. Whoever <see cref="SignIn"/> names is the current user.
/// </summary>
public sealed class TimesheetWorld
{
    public static readonly PunchSource Phone = new("203.0.113.7", "Mobile Safari");

    private readonly Dictionary<UserId, User> _people = [];
    private readonly List<Timesheet> _timesheets = [];
    private readonly List<CorrectionRequest> _requests = [];

    public TimesheetWorld()
    {
        Sam = Person("Sam", Role.Supervisor);
        Sue = Person("Sue", Role.Supervisor);
        Ada = Person("Ada", Role.Admin);
        Abe = Person("Abe", Role.Admin);
        Warehouse = Team.Create("Warehouse", Sam);
        Office = Team.Create("Office", Sue);
        Eve = Person("Eve", Role.Employee, Warehouse);
        Ed = Person("Ed", Role.Employee, Warehouse);
        Olga = Person("Olga", Role.Employee, Office);

        UsersRepository.FindAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(c => _people.GetValueOrDefault(c.Arg<UserId>()));
        UsersRepository.NamesAsync(Arg.Any<IEnumerable<UserId>>(), Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyDictionary<UserId, string>)c.Arg<IEnumerable<UserId>>().ToDictionary(id => id, id => _people[id].Name));
        TeamsRepository.FindAsync(Arg.Any<TeamId>(), Arg.Any<CancellationToken>())
            .Returns(c => new[] { Warehouse, Office }.FirstOrDefault(t => t.Id == c.Arg<TeamId>()));

        TimesheetsRepository.FindAsync(Arg.Any<TimesheetId>(), Arg.Any<CancellationToken>())
            .Returns(c => _timesheets.FirstOrDefault(t => t.Id == c.Arg<TimesheetId>()));
        TimesheetsRepository.FindAsync(Arg.Any<UserId>(), Arg.Any<Week>(), Arg.Any<CancellationToken>())
            .Returns(c => _timesheets.FirstOrDefault(t => t.OwnerId == c.Arg<UserId>() && t.Week == c.Arg<Week>()));
        TimesheetsRepository.FindByEntryAsync(Arg.Any<TimeEntryId>(), Arg.Any<CancellationToken>())
            .Returns(c => _timesheets.FirstOrDefault(t => t.Entries.Any(e => e.Id == c.Arg<TimeEntryId>())));
        TimesheetsRepository.FindWithOpenEntryAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(c => _timesheets.FirstOrDefault(t => t.OwnerId == c.Arg<UserId>() && t.OpenEntry is not null));

        CorrectionsRepository.FindAsync(Arg.Any<CorrectionRequestId>(), Arg.Any<CancellationToken>())
            .Returns(c => _requests.FirstOrDefault(r => r.Id == c.Arg<CorrectionRequestId>()));
        CorrectionsRepository.FindPendingAsync(Arg.Any<TimesheetId>(), Arg.Any<CancellationToken>())
            .Returns(c => _requests.FirstOrDefault(r => r.TimesheetId == c.Arg<TimesheetId>() && r.Status == CorrectionStatus.Pending));
        CorrectionsRepository.FindLatestAsync(Arg.Any<TimesheetId>(), Arg.Any<CancellationToken>())
            .Returns(c => _requests.LastOrDefault(r => r.TimesheetId == c.Arg<TimesheetId>()));

        SignIn(Eve);
    }

    public User Sam { get; }

    public User Sue { get; }

    public User Ada { get; }

    public User Abe { get; }

    public User Eve { get; }

    public User Ed { get; }

    public User Olga { get; }

    public Team Warehouse { get; }

    public Team Office { get; }

    public CompanyClock Clock { get; } = new("2026-10-30 10:00");

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public IUserRepository UsersRepository { get; } = Substitute.For<IUserRepository>();

    public ITeamRepository TeamsRepository { get; } = Substitute.For<ITeamRepository>();

    public TimesheetAccess Access => new(CurrentUser, UsersRepository, TeamsRepository);

    public ITimesheetRepository TimesheetsRepository { get; } = Substitute.For<ITimesheetRepository>();

    public ICorrectionRequestRepository CorrectionsRepository { get; } = Substitute.For<ICorrectionRequestRepository>();

    public static Week WeekOf(int year, int month, int day) => new(new DateOnly(year, month, day));

    public void SignIn(User user) => CurrentUser.Id.Returns(user.Id);

    /// <summary>A Draft of <paramref name="owner"/> with one 4-hour punched entry on the week's Tuesday.</summary>
    public Timesheet Week(User owner, Week week, TimesheetStatus status = TimesheetStatus.Draft)
    {
        var timesheet = Domain.Timesheets.Timesheet.Start(owner.Id, week);
        DateTimeOffset tuesday = CompanyClock.At($"{week.Start.AddDays(1):yyyy-MM-dd} 08:00");
        timesheet.ClockIn(tuesday, Phone, CompanyClock.Zone);
        timesheet.ClockOut(tuesday.AddHours(4), Phone);
        DateTimeOffset ended = week.EndsAt(CompanyClock.Zone);
        if (status != TimesheetStatus.Draft)
        {
            timesheet.Submit(owner.Id, ended, CompanyClock.Zone);
        }

        if (status == TimesheetStatus.Approved)
        {
            timesheet.Approve(Ada.Id, owner.TeamId, ended);
        }

        _timesheets.Add(timesheet);
        return timesheet;
    }

    public void Add(Timesheet timesheet) => _timesheets.Add(timesheet);

    public CorrectionRequest PendingRequest(Timesheet timesheet)
    {
        var request = CorrectionRequest.Create(timesheet.Id, timesheet.OwnerId, "Wednesday shift is missing", Clock.GetUtcNow());
        _requests.Add(request);
        return request;
    }

    public void Move(User employee, Team team) => employee.MoveTo(team);

    private User Person(string name, Role role, Team? team = null)
    {
        var user = User.Create(new Email($"{name.ToLowerInvariant()}@clockin.test"), name, role);
        if (team is not null)
        {
            user.MoveTo(team);
        }

        _people[user.Id] = user;
        return user;
    }
}
