using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;

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
        exceptions.EnumerateArray().Select(e => e.GetProperty("kind").GetString()).Should().Contain("LongEntry");
        samToDos.GetProperty("timesheetsToReview").GetInt32().Should().Be(1);
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        string text = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync(Ct));
        text.Should().Contain($",{week.AddDays(1):yyyy-MM-dd},08:00,19:00,11:00,11.00,true,\"long day, client, on site\"\r\n");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> TeamAsync(User supervisor, User employee)
    {
        using var scopes = new Scopes(factory);
        var team = Team.Create($"Team {Guid.CreateVersion7():N}", supervisor);
        await scopes.Get<ITeamRepository>().SaveAsync(team, Ct);
        IUserRepository users = scopes.Get<IUserRepository>();
        User saved = (await users.FindAsync(employee.Id, Ct))!;
        saved.MoveTo(team);
        await users.SaveAsync(saved, Ct);
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
