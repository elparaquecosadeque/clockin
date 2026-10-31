using ClockIn.Application.Demo.Interfaces;
using ClockIn.Application.Demo.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;
using ClockIn.Domain.Timesheets;
using ClockIn.Persistence.Accounts;
using ClockIn.Persistence.Common;
using ClockIn.Persistence.Teams;
using ClockIn.Persistence.Timesheets;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;

namespace ClockIn.Persistence.Seeds;

/// <summary>
/// Demo and development data (ADR 0005): wipes every table and rebuilds a company of 2 admins, 3 supervisors
/// and 15 employees, with history and today's punches relative to now in company time.
/// </summary>
/// <remarks>
/// Writes records directly, below the domain, so it must follow the aggregates' rules by hand:
/// entries never overlap, at most one is open per person, and a week with an open entry is Draft.
/// The seed grows with the features: returned timesheets and correction requests come with them.
/// </remarks>
public sealed class DemoSeed(AppDataConnection db, TimeProvider time) : IDemoData
{
    private const string DemoPassword = "clockin-demo";

    public const string EmailDomain = "clockin.demo";

    private const string Warehouse = "Warehouse";
    private const string Office = "Office";
    private const string NightShift = "Night Shift";
    private const string OldStore = "Old Store";

    private static readonly Person[] People =
    [
        new("Ada", Role.Admin, Today: DemoScenario.Admin, Featured: true),
        new("Abe", Role.Admin, Today: DemoScenario.Admin),
        new("Sam", Role.Supervisor, Leads: Warehouse, Today: DemoScenario.ClockedIn, OpenFor: TimeSpan.FromHours(2), Featured: true),
        new("Sue", Role.Supervisor, Leads: Office, Today: DemoScenario.ClockedOut, Featured: true),
        new("Sid", Role.Supervisor, Leads: NightShift),
        new("Eve", Role.Employee, Team: Warehouse, Today: DemoScenario.ClockedIn, OpenFor: TimeSpan.FromHours(2), Featured: true),
        new("Ed", Role.Employee, Team: Warehouse, Status: UserStatus.Deactivated, Today: DemoScenario.Deactivated),
        new("Fay", Role.Employee, Team: Warehouse, Status: UserStatus.PendingActivation, Today: DemoScenario.PendingActivation),
        new("Gus", Role.Employee, Team: Warehouse, Today: DemoScenario.ClockedIn, OpenFor: TimeSpan.FromHours(3)),
        new("Hal", Role.Employee, Team: Warehouse, Today: DemoScenario.ClockedOut, Featured: true),
        new("Ivy", Role.Employee, Team: Office, Today: DemoScenario.ClockedIn, OpenFor: TimeSpan.FromHours(1)),
        new("Jon", Role.Employee, Team: Office, Today: DemoScenario.ClockedOut),
        new("Kim", Role.Employee, Team: Office, Today: DemoScenario.ClockedIn, OpenFor: TimeSpan.FromHours(4)),
        new("Lou", Role.Employee, Team: Office, Today: DemoScenario.LockedOut),
        new("Mia", Role.Employee, Team: Office, Featured: true),
        new("Ned", Role.Employee, Team: NightShift, Today: DemoScenario.ClockedIn, OpenFor: TimeSpan.FromHours(9)),
        new("Oli", Role.Employee, Team: NightShift, Today: DemoScenario.ForgotToClockOut, OpenFor: TimeSpan.FromHours(20), Featured: true),
        new("Pam", Role.Employee, Team: NightShift, Today: DemoScenario.ClockedOut),
        new("Ray", Role.Employee, Team: NightShift),
        new("Rob", Role.Employee, Team: NightShift),
    ];

    private static readonly IReadOnlyList<DemoAccount> Featured =
        [.. People.Where(p => p.Featured).Select(p => new DemoAccount(p.Email, p.Name, p.Role, p.Team ?? p.Leads, p.Today))];

    public string Password => DemoPassword;

    public IReadOnlyList<DemoAccount> FeaturedAccounts => Featured;

    public async Task<DateTimeOffset?> LastSeededAtAsync(CancellationToken ct) =>
        await db.GetTable<DemoSeedRecord>().Select(s => (DateTimeOffset?)s.SeededAt).FirstOrDefaultAsync(ct);

    public async Task ResetAsync(string passwordHash, CancellationToken ct)
    {
        DateTimeOffset now = time.GetUtcNow();
        Company company = Build(passwordHash, now, time.LocalTimeZone);

        await using DataConnectionTransaction transaction = await db.BeginTransactionAsync(ct);
        await WipeAsync(ct);
        foreach (UserRecord user in company.Users)
        {
            await db.InsertAsync(user, token: ct);
        }

        foreach (TeamRecord team in company.Teams)
        {
            await db.InsertAsync(team, token: ct);
        }

        foreach ((Guid userId, Guid teamId) in company.Memberships)
        {
            await db.GetTable<UserRecord>().Where(u => u.Id == userId).Set(u => u.TeamId, teamId).UpdateAsync(ct);
        }

        foreach (TimesheetRecord timesheet in company.Timesheets)
        {
            await db.InsertAsync(timesheet, token: ct);
        }

        foreach (TimeEntryRecord entry in company.Entries)
        {
            await db.InsertAsync(entry, token: ct);
        }

        await db.InsertAsync(new DemoSeedRecord { SeededAt = now }, token: ct);
        await transaction.CommitAsync(ct);
    }

    private async Task WipeAsync(CancellationToken ct)
    {
        await db.GetTable<CorrectionRequestRecord>().DeleteAsync(ct);
        await db.GetTable<TimesheetHistoryRecord>().DeleteAsync(ct);
        await db.GetTable<TimeEntryRecord>().DeleteAsync(ct);
        await db.GetTable<TimesheetRecord>().DeleteAsync(ct);
        await db.GetTable<SessionRecord>().DeleteAsync(ct);
        await db.GetTable<UserRecord>().Set(u => u.TeamId, (Guid?)null).UpdateAsync(ct);
        await db.GetTable<TeamRecord>().DeleteAsync(ct);
        await db.GetTable<UserRecord>().DeleteAsync(ct);
        await db.GetTable<DemoSeedRecord>().DeleteAsync(ct);
    }

    private static Company Build(string passwordHash, DateTimeOffset now, TimeZoneInfo zone)
    {
        var company = new Company();
        Dictionary<string, Guid> userIds = People.ToDictionary(p => p.Name, _ => Guid.CreateVersion7());
        Dictionary<string, Guid> teamIds = new[] { Warehouse, Office, NightShift, OldStore }.ToDictionary(t => t, _ => Guid.CreateVersion7());

        company.Teams.AddRange(People.Where(p => p.Leads is not null).Select(p => TeamRecordFor(teamIds[p.Leads!], p.Leads!, userIds[p.Name], archived: false)));
        // Archived, with no members; it keeps its name taken and shows up in history (archive-team.feature).
        company.Teams.Add(TeamRecordFor(teamIds[OldStore], OldStore, userIds["Sam"], archived: true));

        var timeline = new Timeline(now, zone);
        for (int i = 0; i < People.Length; i++)
        {
            Person person = People[i];
            Guid userId = userIds[person.Name];
            company.Users.Add(UserRecordFor(person, userId, passwordHash, now));
            if (person.Team is not null)
            {
                company.Memberships.Add((userId, teamIds[person.Team]));
            }
            if (person.Role != Role.Admin && person.Status != UserStatus.PendingActivation)
            {
                AddTime(company, person, userId, ipSuffix: 10 + i, timeline);
            }
        }

        return company;
    }

    private static void AddTime(Company company, Person person, Guid userId, int ipSuffix, Timeline timeline)
    {
        // Small personal offsets, so not everyone punches on the same minute.
        int offset = (ipSuffix * 7 % 21) - 10;
        var entries = new List<Entry>();
        entries.AddRange(Today(person, timeline));
        DateOnly firstTodayDay = entries.Count == 0 ? timeline.Today : entries.Min(e => timeline.DayOf(e.Start));

        int weeks = person.Status == UserStatus.Deactivated ? 1 : 3;
        foreach (DateOnly week in timeline.Weeks.Take(weeks))
        {
            for (DateOnly day = week; day < week.AddDays(5) && day < firstTodayDay; day = day.AddDays(1))
            {
                entries.AddRange(Workday(person, day, week, offset, timeline));
            }
        }

        string ip = $"203.0.113.{ipSuffix}";
        foreach (IGrouping<DateOnly, Entry> week in entries.GroupBy(e => timeline.WeekOf(e.Start)))
        {
            var timesheetId = Guid.CreateVersion7();
            bool hasOpenEntry = week.Any(e => e.End is null);
            company.Timesheets.Add(new TimesheetRecord
            {
                Id = timesheetId,
                OwnerId = userId,
                WeekStart = week.Key,
                Status = (hasOpenEntry ? TimesheetStatus.Draft : StatusOf(person, week.Key, timeline)).ToString(),
                Version = 1,
            });
            company.Entries.AddRange(week.Select(e => new TimeEntryRecord
            {
                Id = Guid.CreateVersion7(),
                TimesheetId = timesheetId,
                OwnerId = userId,
                StartedAt = e.Start,
                EndedAt = e.End,
                ClockInIp = ip,
                ClockInUserAgent = UserAgent,
                ClockOutIp = e.End is null ? null : ip,
                ClockOutUserAgent = e.End is null ? null : UserAgent,
                IsManuallyChanged = e.IsManuallyChanged,
                ManualChange = e.IsManuallyChanged ? nameof(ManualChange.Added) : nameof(ManualChange.None),
                ChangedBy = e.IsManuallyChanged ? userId : null,
                Note = e.IsManuallyChanged ? "Forgot to clock in" : null,
            }));
        }
    }

    private const string UserAgent = "Mozilla/5.0 (ClockIn demo data)";

    private static IEnumerable<Entry> Today(Person person, Timeline timeline)
    {
        DateTimeOffset now = timeline.Now;
        return person.Today switch
        {
            DemoScenario.ClockedIn or DemoScenario.ForgotToClockOut => [new Entry(now - person.OpenFor, null)],
            DemoScenario.ClockedOut =>
            [
                new Entry(now.AddHours(-8), now.AddHours(-4)),
                new Entry(now.AddHours(-3.5), now.AddMinutes(-30)),
            ],
            _ => [],
        };
    }

    private static IEnumerable<Entry> Workday(Person person, DateOnly day, DateOnly week, int offset, Timeline timeline)
    {
        bool lastWeek = week == timeline.Weeks[1];

        // An entry over 10 hours, flagged for the supervisor (exceptions in the requirements).
        if (person.Name == "Gus" && lastWeek && day == week.AddDays(1))
        {
            return [new Entry(timeline.At(day, 7, 0), timeline.At(day, 18, 30))];
        }

        // Many manual changes in one week: 3 or more is flagged.
        bool manual = person.Name == "Kim" && lastWeek && day < week.AddDays(3);
        return
        [
            new Entry(timeline.At(day, 8, offset), timeline.At(day, 12, offset), manual),
            new Entry(timeline.At(day, 13, offset), timeline.At(day, 17, offset)),
        ];
    }

    /// <summary>Two weeks ago is approved; last week waits in the approvers' queues; this week is open.</summary>
    private static TimesheetStatus StatusOf(Person person, DateOnly week, Timeline timeline)
    {
        if (week == timeline.Weeks[0])
        {
            return TimesheetStatus.Approved;
        }

        if (week != timeline.Weeks[1])
        {
            return TimesheetStatus.Draft;
        }

        return person.Name switch
        {
            "Mia" or "Sid" => TimesheetStatus.Draft, // forgot to submit
            _ when person.Team == Office || person.Name == "Sue" => TimesheetStatus.Approved,
            _ => TimesheetStatus.Submitted,
        };
    }

    private static UserRecord UserRecordFor(Person person, Guid id, string passwordHash, DateTimeOffset now) => new()
    {
        Id = id,
        Email = person.Email,
        Name = person.Name,
        Role = person.Role.ToString(),
        Status = person.Status.ToString(),
        PasswordHash = person.Status == UserStatus.PendingActivation ? null : passwordHash,
        SecurityStamp = Guid.CreateVersion7(),
        FailedSignIns = 0,
        LockedUntil = person.Today == DemoScenario.LockedOut ? now + User.LockoutLength : null,
        AccessLinkHash = null,
        AccessLinkExpiresAt = null,
        TeamId = null,
        Language = Preferences.English,
        TimeFormat = Preferences.TwentyFourHour,
        Version = 1,
    };

    private static TeamRecord TeamRecordFor(Guid id, string name, Guid supervisorId, bool archived) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = Team.NormalizeName(name),
        SupervisorId = supervisorId,
        IsArchived = archived,
        Version = 1,
    };

    private sealed record Person(
        string Name,
        Role Role,
        string? Team = null,
        string? Leads = null,
        UserStatus Status = UserStatus.Active,
        DemoScenario Today = DemoScenario.NotStarted,
        TimeSpan OpenFor = default,
        bool Featured = false)
    {
        public string Email => $"{Name.ToLowerInvariant()}@{EmailDomain}";
    }

    private sealed record Entry(DateTimeOffset Start, DateTimeOffset? End, bool IsManuallyChanged = false);

    private sealed class Company
    {
        public List<UserRecord> Users { get; } = [];

        public List<TeamRecord> Teams { get; } = [];

        /// <summary>Users and teams reference each other, so memberships are set after both exist.</summary>
        public List<(Guid UserId, Guid TeamId)> Memberships { get; } = [];

        public List<TimesheetRecord> Timesheets { get; } = [];

        public List<TimeEntryRecord> Entries { get; } = [];
    }

    /// <summary>Days and weeks in company time, counted back from now.</summary>
    private sealed class Timeline(DateTimeOffset now, TimeZoneInfo zone)
    {
        public DateTimeOffset Now { get; } = now;

        public DateOnly Today => DayOf(Now);

        /// <summary>Two weeks ago, last week, this week (Mondays).</summary>
        public IReadOnlyList<DateOnly> Weeks { get; } =
            [.. new[] { -14, -7, 0 }.Select(d => Week.Containing(now, zone).Start.AddDays(d))];

        public DateOnly DayOf(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

        public DateOnly WeekOf(DateTimeOffset instant) => Week.Containing(instant, zone).Start;

        public DateTimeOffset At(DateOnly day, int hour, int minute)
        {
            DateTime local = day.ToDateTime(new TimeOnly(hour, 0)).AddMinutes(minute);
            return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        }
    }
}
