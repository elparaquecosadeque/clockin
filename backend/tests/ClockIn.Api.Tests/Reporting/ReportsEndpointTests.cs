using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Api.Tests.Reporting;

/// <summary>The reports over HTTP and the real reads: one team from submission to export.</summary>
[Collection(SharedApi.Name)]
public sealed class ReportsEndpointTests(ApiFactory factory)
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reports_a_team_from_submission_to_export()
    {
        User sam = await CreateAsync(Role.Supervisor);
        User eve = await CreateAsync(Role.Employee);
        Guid teamId = await TeamAsync(sam, eve);
        HttpClient asEve = await SignedInAsync(eve);
        HttpClient asSam = await SignedInAsync(sam);
        DateOnly week = PastMonday(2);
        await asEve.PostAsJsonAsync("/api/time-entries", new { start = At(week, 1, 8), end = At(week, 1, 19), note = "long day, client, on site" }, Ct);
        await asEve.PostAsync($"/api/timesheets/mine/{week:yyyy-MM-dd}/submit", null, Ct);
        await asEve.PostAsync("/api/time-entries/clock-in", null, Ct);

        JsonElement queue = await JsonAsync(asSam, "/api/reports/review-queue");
        JsonElement grid = await JsonAsync(asSam, $"/api/reports/team-week/{week:yyyy-MM-dd}");
        JsonElement clockedIn = await JsonAsync(asSam, "/api/reports/clocked-in");
        JsonElement exceptions = await JsonAsync(asSam, "/api/reports/exceptions");
        JsonElement samToDos = await JsonAsync(asSam, "/api/me/to-dos");
        Guid timesheetId = queue.GetProperty("timesheets")[0].GetProperty("timesheetId").GetGuid();
        await asSam.PostAsync($"/api/timesheets/{timesheetId}/approve", null, Ct);
        HttpResponseMessage csv = await asSam.GetAsync($"/api/reports/approved-csv/{week:yyyy-MM-dd}", Ct);
        HttpResponseMessage forbidden = await asEve.GetAsync("/api/reports/clocked-in", Ct);

        queue.GetProperty("timesheets").GetArrayLength().Should().Be(1);
        JsonElement row = grid.GetProperty("groups")[0].GetProperty("rows")[0];
        row.GetProperty("status").GetString().Should().Be("Submitted");
        row.GetProperty("daySeconds")[1].GetInt64().Should().Be(11 * 3600);
        grid.GetProperty("groups")[0].GetProperty("teamId").GetGuid().Should().Be(teamId);
        clockedIn.GetArrayLength().Should().Be(1);
        exceptions.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("kind").GetString()).Should().Contain("LongEntry");
        samToDos.GetProperty("timesheetsToReview").GetInt32().Should().Be(1);
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        string text = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync(Ct));
        text.Should().Contain($",{week.AddDays(1):yyyy-MM-dd},08:00,19:00,11:00,11.00,true,\"long day, client, on site\"\r\n");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pages_old_unresolved_work_without_loading_it_into_badges_or_clocked_in_lists()
    {
        User sam = await CreateAsync(Role.Supervisor);
        User eve = await CreateAsync(Role.Employee);
        await TeamAsync(sam, eve);
        using (var scopes = new Scopes(factory))
        {
            ITimesheetRepository repository = scopes.Get<ITimesheetRepository>();
            for (int i = 1; i <= 52; i++)
            {
                var sheet = Timesheet.Start(eve.Id, new Week(PastMonday(i)));
                await repository.SaveOrUpdateAsync(sheet, Ct);
            }
        }
        HttpClient client = await SignedInAsync(sam);

        JsonElement first = await JsonAsync(client, "/api/reports/exceptions?page=1");
        JsonElement second = await JsonAsync(client, "/api/reports/exceptions?page=2");
        JsonElement badges = await JsonAsync(client, "/api/me/to-dos");
        JsonElement clockedIn = await JsonAsync(client, "/api/reports/clocked-in");

        first.GetProperty("items").GetArrayLength().Should().Be(50);
        first.GetProperty("hasMore").GetBoolean().Should().BeTrue();
        second.GetProperty("items").GetArrayLength().Should().Be(2);
        second.GetProperty("hasMore").GetBoolean().Should().BeFalse();
        first.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("timesheetId").GetGuid())
            .Should().NotIntersectWith(second.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("timesheetId").GetGuid()));
        badges.GetProperty("timesheetsToReview").GetInt32().Should().Be(0);
        clockedIn.GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("/api/reports/exceptions?page=0")]
    [InlineData("/api/reports/exceptions?page=2147483647")]
    [InlineData("/api/reports/review-queue?timesheetPage=-1")]
    [InlineData("/api/reports/review-queue?correctionPage=0")]
    public async Task Rejects_invalid_pagination(string path)
    {
        User sam = await CreateAsync(Role.Supervisor);
        HttpClient client = await SignedInAsync(sam);

        (await client.GetAsync(path, Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pages_submissions_and_corrections_independently_but_counts_the_entire_authorized_queue()
    {
        User sam = await CreateAsync(Role.Supervisor);
        User eve = await CreateAsync(Role.Employee);
        User outsider = await CreateAsync(Role.Employee);
        await TeamAsync(sam, eve);
        DateTimeOffset now = TimeProvider.System.GetUtcNow();
        using (var scopes = new Scopes(factory))
        {
            ITimesheetRepository sheets = scopes.Get<ITimesheetRepository>();
            ICorrectionRequestRepository requests = scopes.Get<ICorrectionRequestRepository>();
            for (int i = 1; i <= 52; i++)
            {
                var sheet = Timesheet.Start(eve.Id, new Week(PastMonday(i)));
                sheet.Submit(eve.Id, now, Bogota);
                await sheets.SaveOrUpdateAsync(sheet, Ct);
                await requests.SaveOrUpdateAsync(CorrectionRequest.Create(sheet.Id, eve.Id, "Missing shift", now), Ct);
            }
            var other = Timesheet.Start(outsider.Id, new Week(PastMonday(1)));
            other.Submit(outsider.Id, now, Bogota);
            await sheets.SaveOrUpdateAsync(other, Ct);
            await requests.SaveOrUpdateAsync(CorrectionRequest.Create(other.Id, outsider.Id, "Private", now), Ct);
        }
        HttpClient client = await SignedInAsync(sam);

        JsonElement first = await JsonAsync(client, "/api/reports/review-queue");
        JsonElement nextSheets = await JsonAsync(client, "/api/reports/review-queue?timesheetPage=2");
        JsonElement nextRequests = await JsonAsync(client, "/api/reports/review-queue?correctionPage=2");
        JsonElement badges = await JsonAsync(client, "/api/me/to-dos");

        first.GetProperty("timesheets").GetArrayLength().Should().Be(50);
        first.GetProperty("corrections").GetArrayLength().Should().Be(50);
        first.GetProperty("hasMoreTimesheets").GetBoolean().Should().BeTrue();
        first.GetProperty("hasMoreCorrections").GetBoolean().Should().BeTrue();
        nextSheets.GetProperty("timesheets").GetArrayLength().Should().Be(2);
        nextSheets.GetProperty("hasMoreTimesheets").GetBoolean().Should().BeFalse();
        nextSheets.GetProperty("corrections").GetRawText().Should().Be(first.GetProperty("corrections").GetRawText());
        nextRequests.GetProperty("corrections").GetArrayLength().Should().Be(2);
        nextRequests.GetProperty("hasMoreCorrections").GetBoolean().Should().BeFalse();
        first.GetProperty("timesheets").EnumerateArray().Select(t => t.GetProperty("timesheetId").GetGuid()).Should()
            .NotIntersectWith(nextSheets.GetProperty("timesheets").EnumerateArray().Select(t => t.GetProperty("timesheetId").GetGuid()));
        first.GetProperty("corrections").EnumerateArray().Select(t => t.GetProperty("requestId").GetGuid()).Should()
            .NotIntersectWith(nextRequests.GetProperty("corrections").EnumerateArray().Select(t => t.GetProperty("requestId").GetGuid()));
        badges.GetProperty("timesheetsToReview").GetInt32().Should().Be(52);
        badges.GetProperty("correctionsToDecide").GetInt32().Should().Be(52);
        nextSheets.GetProperty("timesheets").EnumerateArray().Should().OnlyContain(t => t.GetProperty("ownerId").GetGuid() == eve.Id.Value);
        nextRequests.GetProperty("corrections").EnumerateArray().Should().OnlyContain(t => t.GetProperty("ownerId").GetGuid() == eve.Id.Value);
    }

    [Fact]
    public async Task Computes_exception_thresholds_in_sql_and_excludes_approved_and_other_team_weeks()
    {
        User sam = await CreateAsync(Role.Supervisor);
        User eve = await CreateAsync(Role.Employee);
        User outsider = await CreateAsync(Role.Employee);
        Guid teamId = await TeamAsync(sam, eve);
        DateTimeOffset now = TimeProvider.System.GetUtcNow();
        using (var scopes = new Scopes(factory))
        {
            ITimesheetRepository sheets = scopes.Get<ITimesheetRepository>();
            foreach ((User owner, int ago) in new[] { (eve, 1), (eve, 2), (outsider, 1) })
            {
                DateOnly week = PastMonday(ago);
                var sheet = Timesheet.Start(owner.Id, new Week(week));
                var source = new PunchSource("127.0.0.1", "report test");
                sheet.AddEntry(owner.Id, At(week, 0, 8), At(week, 0, 18), "exactly ten hours", source, now, Bogota, []);
                sheet.AddEntry(owner.Id, At(week, 1, 8), At(week, 1, 19), "eleven hours", source, now, Bogota, []);
                sheet.AddEntry(owner.Id, At(week, 2, 8), At(week, 2, 9), "third manual entry", source, now, Bogota, []);
                sheet.Submit(owner.Id, now, Bogota);
                if (ago == 2)
                {
                    sheet.Approve(sam.Id, new TeamId(teamId), now);
                }
                await sheets.SaveOrUpdateAsync(sheet, Ct);
            }
        }
        HttpClient client = await SignedInAsync(sam);

        JsonElement result = await JsonAsync(client, "/api/reports/exceptions");
        JsonElement queue = await JsonAsync(client, "/api/reports/review-queue");

        JsonElement[] items = result.GetProperty("items").EnumerateArray().ToArray();
        items.Should().HaveCount(2).And.OnlyContain(e => e.GetProperty("userId").GetGuid() == eve.Id.Value);
        items.Single(e => e.GetProperty("kind").GetString() == "LongEntry").GetProperty("seconds").GetInt64().Should().Be(11 * 3600);
        items.Single(e => e.GetProperty("kind").GetString() == "ManyManualChanges").GetProperty("count").GetInt32().Should().Be(3);
        queue.GetProperty("timesheets")[0].GetProperty("totalSeconds").GetInt64().Should().Be(22 * 3600);
    }

    private async Task<Guid> TeamAsync(User supervisor, User employee)
    {
        using var scopes = new Scopes(factory);
        var team = Team.Create($"Team {Guid.CreateVersion7():N}", supervisor);
        await scopes.Get<ITeamRepository>().SaveOrUpdateAsync(team, Ct);
        IUserRepository usersRepository = scopes.Get<IUserRepository>();
        User saved = (await usersRepository.FindAsync(employee.Id, Ct))!;
        saved.MoveTo(team);
        await usersRepository.SaveOrUpdateAsync(saved, Ct);
        return team.Id.Value;
    }

    private static DateOnly PastMonday(int weeksAgo)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(TimeProvider.System.GetUtcNow(), Bogota).DateTime);
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7) - (7 * weeksAgo));
    }

    private static DateTimeOffset At(DateOnly monday, int dayOffset, int hour) =>
        new(monday.AddDays(dayOffset).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.FromHours(-5));

    private Task<User> CreateAsync(Role role) => TestUsers.CreateActiveAsync(factory, role, Ct);

    private Task<HttpClient> SignedInAsync(User user) => TestUsers.SignedInClientAsync(factory, user, Ct);

    private async Task<JsonElement> JsonAsync(HttpClient client, string path)
    {
        HttpResponseMessage response = await client.GetAsync(path, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
    }
}
