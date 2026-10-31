using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Demo.Results;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using ClockIn.Persistence.Seeds;
using ClockIn.Tests.Common;
using Microsoft.AspNetCore.Hosting;

namespace ClockIn.Api.Tests.Demo;

/// <summary>ADR 0005: the demo company, as seeded on startup at Friday 2026-10-30 10:00.</summary>
public sealed class DemoDataTests(DemoApiFactory factory) : IClassFixture<DemoApiFactory>, IDisposable
{
    private static readonly Week ThisWeek = new(new DateOnly(2026, 10, 26));
    private static readonly Week LastWeek = new(new DateOnly(2026, 10, 19));
    private static readonly Week TwoWeeksAgo = new(new DateOnly(2026, 10, 12));

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly Scopes _scopes = new(factory);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _scopes.Dispose();

    [Fact]
    public async Task Seeds_three_teams_each_led_by_a_supervisor_with_five_employees()
    {
        IReadOnlyList<User> employees = await Users().FindByRoleAsync(Role.Employee, Ct);
        IReadOnlyList<User> supervisors = await Users().FindByRoleAsync(Role.Supervisor, Ct);

        (await Users().FindByRoleAsync(Role.Admin, Ct)).Should().HaveCount(2);
        supervisors.Should().HaveCount(3).And.OnlyContain(s => s.TeamId == null);
        employees.Should().HaveCount(15);
        employees.GroupBy(e => e.TeamId).Select(g => g.Count()).Should().Equal(5, 5, 5);
        foreach (User supervisor in supervisors)
        {
            (await Teams().LeadsActiveTeamAsync(supervisor.Id, Ct)).Should().BeTrue();
        }

        (await Teams().NameInUseAsync("Old Store", Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Has_people_clocked_in_clocked_out_and_one_who_forgot_to_clock_out()
    {
        TimeEntry eves = (await Timesheets().FindWithOpenEntryAsync((await UserAsync("eve")).Id, Ct))!.OpenEntry!;
        TimeEntry olis = (await Timesheets().FindWithOpenEntryAsync((await UserAsync("oli")).Id, Ct))!.OpenEntry!;
        User hal = await UserAsync("hal");
        Timesheet hals = (await Timesheets().FindAsync(hal.Id, ThisWeek, Ct))!;

        eves.Start.Should().Be(CompanyClock.At("2026-10-30 08:00"));
        (factory.Clock.GetUtcNow() - olis.Start).Should().BeGreaterThan(TimeEntry.MaxLength);
        (await Timesheets().FindWithOpenEntryAsync(hal.Id, Ct)).Should().BeNull();
        hals.Entries.Should().Contain(e => e.Start >= CompanyClock.At("2026-10-30 00:00"));
    }

    [Fact]
    public async Task Keeps_every_persons_entries_apart_with_at_most_one_open()
    {
        IEnumerable<User> trackers = (await Users().FindByRoleAsync(Role.Employee, Ct))
            .Concat(await Users().FindByRoleAsync(Role.Supervisor, Ct));
        foreach (User user in trackers)
        {
            List<TimeEntry> entries = [];
            foreach (Week week in new[] { TwoWeeksAgo, LastWeek, ThisWeek })
            {
                entries.AddRange((await Timesheets().FindAsync(user.Id, week, Ct))?.Entries ?? []);
            }

            entries.Count(e => e.End is null).Should().BeLessThanOrEqualTo(1, user.Name);
            List<TimeEntry> sorted = [.. entries.OrderBy(e => e.Start)];
            sorted.Zip(sorted.Skip(1)).Should().OnlyContain(pair => pair.First.End <= pair.Second.Start, user.Name);
        }
    }

    [Fact]
    public async Task Leaves_last_weeks_timesheets_in_the_approvers_queues_and_flags_some()
    {
        (await StatusAsync("eve", LastWeek)).Should().Be(TimesheetStatus.Submitted);
        (await StatusAsync("sam", LastWeek)).Should().Be(TimesheetStatus.Submitted);
        (await StatusAsync("ivy", LastWeek)).Should().Be(TimesheetStatus.Approved);
        (await StatusAsync("mia", LastWeek)).Should().Be(TimesheetStatus.Draft);
        (await StatusAsync("eve", TwoWeeksAgo)).Should().Be(TimesheetStatus.Approved);
        (await StatusAsync("eve", ThisWeek)).Should().Be(TimesheetStatus.Draft);

        Timesheet gus = (await Timesheets().FindAsync((await UserAsync("gus")).Id, LastWeek, Ct))!;
        Timesheet kim = (await Timesheets().FindAsync((await UserAsync("kim")).Id, LastWeek, Ct))!;
        gus.Entries.Should().Contain(e => e.Duration > TimeSpan.FromHours(10));
        kim.Entries.Count(e => e.IsManuallyChanged).Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Records_approvals_and_has_a_returned_week_and_a_pending_correction()
    {
        User eve = await UserAsync("eve");
        Timesheet eves = (await Timesheets().FindAsync(eve.Id, TwoWeeksAgo, Ct))!;
        Timesheet sams = (await Timesheets().FindAsync((await UserAsync("sam")).Id, TwoWeeksAgo, Ct))!;
        Timesheet hals = (await Timesheets().FindAsync((await UserAsync("hal")).Id, LastWeek, Ct))!;
        Timesheet jons = (await Timesheets().FindAsync((await UserAsync("jon")).Id, TwoWeeksAgo, Ct))!;

        eves.Approval!.ApproverId.Should().Be((await UserAsync("sam")).Id);
        eves.Approval.TeamId.Should().Be(eve.TeamId);
        eves.History.Select(h => h.Kind).Should().Equal(TimesheetEventKind.Submitted, TimesheetEventKind.Approved);
        sams.Approval!.ApproverId.Should().Be((await UserAsync("ada")).Id);

        hals.Status.Should().Be(TimesheetStatus.Draft);
        hals.ReturnComment.Should().NotBeNullOrWhiteSpace();
        hals.History.Select(h => h.Kind).Should().Equal(TimesheetEventKind.Submitted, TimesheetEventKind.Returned);

        (await Corrections().FindPendingAsync(jons.Id, Ct)).Should().NotBeNull();
        jons.History[^1].Kind.Should().Be(TimesheetEventKind.CorrectionRequested);
    }

    [Fact]
    public async Task Lists_featured_accounts_that_sign_in_with_the_shared_password()
    {
        HttpClient client = TestUsers.HttpsClient(factory);

        DemoAccountsDto? demo = await client.GetFromJsonAsync<DemoAccountsDto>("/api/demo/accounts", Json, Ct);

        demo!.Accounts.Select(a => a.Scenario).Should().Contain([DemoScenario.Admin, DemoScenario.ClockedIn, DemoScenario.ForgotToClockOut]);
        foreach (DemoAccount account in demo.Accounts)
        {
            HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new { email = account.Email, password = demo.Password }, Ct);
            login.StatusCode.Should().Be(HttpStatusCode.OK, account.Email);
        }
    }

    [Fact]
    public void Refuses_to_start_with_demo_data_outside_development_and_demo()
    {
        var start = () => factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

        start.Should().Throw<Exception>().Where(e => e.ToString().Contains("Demo:Enabled"));
    }

    private async Task<User> UserAsync(string name) =>
        (await Users().FindByEmailAsync(new Email($"{name}@{DemoSeed.EmailDomain}"), Ct))!;

    private async Task<TimesheetStatus?> StatusAsync(string name, Week week) =>
        (await Timesheets().FindAsync((await UserAsync(name)).Id, week, Ct))?.Status;

    private IUserRepository Users() => _scopes.Get<IUserRepository>();

    private ITeamRepository Teams() => _scopes.Get<ITeamRepository>();

    private ITimesheetRepository Timesheets() => _scopes.Get<ITimesheetRepository>();

    private ICorrectionRequestRepository Corrections() => _scopes.Get<ICorrectionRequestRepository>();
}
