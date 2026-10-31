using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;

namespace ClockIn.Api.Tests.Timesheets;

/// <summary>
/// The timesheet workflow over HTTP: manual entries, submit, approve, corrections. Weeks lie in the past of the real
/// clock so they have ended; each test uses its own people.
/// </summary>
[Collection(SharedApi.Name)]
public sealed class TimesheetWorkflowEndpointTests(ApiFactory factory)
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Adds_edits_and_deletes_entries_and_shows_them_with_their_history()
    {
        (HttpClient eve, _, _) = await TeamAsync();
        DateOnly week = PastMonday(2);

        Guid entryId = await AddAsync(eve, week, 8, 12);
        JsonElement afterAdd = await TimesheetAsync(eve, $"mine/{week:yyyy-MM-dd}");
        HttpResponseMessage edit = await eve.PutAsJsonAsync(
            $"/api/time-entries/{entryId}", new { start = At(week, 1, 7), end = At(week, 1, 12), note = "badge was slow", version = Version(afterAdd) }, Ct);
        HttpResponseMessage stale = await eve.PutAsJsonAsync(
            $"/api/time-entries/{entryId}", new { start = At(week, 1, 6), end = At(week, 1, 12), note = "again", version = Version(afterAdd) }, Ct);
        JsonElement afterEdit = await TimesheetAsync(eve, $"mine/{week:yyyy-MM-dd}");
        HttpResponseMessage delete = await eve.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/time-entries/{entryId}")
        {
            Content = JsonContent.Create(new { note = "duplicate", version = Version(afterEdit) }),
        }, Ct);
        JsonElement afterDelete = await TimesheetAsync(eve, $"mine/{week:yyyy-MM-dd}");

        afterAdd.GetProperty("totalSeconds").GetInt64().Should().Be(4 * 3600);
        afterAdd.GetProperty("entries")[0].GetProperty("manualChange").GetString().Should().Be("Added");
        edit.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        afterEdit.GetProperty("totalSeconds").GetInt64().Should().Be(5 * 3600);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterDelete.GetProperty("entries").GetArrayLength().Should().Be(0);
        afterDelete.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("kind").GetString())
            .Should().Equal("EntryAdded", "EntryEdited", "EntryDeleted");
        afterDelete.GetProperty("history")[2].GetProperty("before").GetProperty("start").GetDateTimeOffset().Should().Be(At(week, 1, 7));
    }

    [Fact]
    public async Task Submits_and_lets_the_team_supervisor_approve_once_when_two_approvers_act_together()
    {
        (HttpClient eve, HttpClient sam, Guid eveId) = await TeamAsync();
        HttpClient ada = await SignedInAsync(await CreateAsync(Role.Admin));
        DateOnly week = PastMonday(2);
        await AddAsync(eve, week, 8, 16);

        HttpResponseMessage submit = await eve.PostAsync($"/api/timesheets/mine/{week:yyyy-MM-dd}/submit", null, Ct);
        Guid id = (await TimesheetAsync(sam, $"users/{eveId}/{week:yyyy-MM-dd}")).GetProperty("id").GetGuid();
        HttpResponseMessage[] approvals = await Task.WhenAll(
            sam.PostAsync($"/api/timesheets/{id}/approve", null, Ct),
            ada.PostAsync($"/api/timesheets/{id}/approve", null, Ct));
        JsonElement approved = await TimesheetAsync(eve, $"mine/{week:yyyy-MM-dd}");

        submit.StatusCode.Should().Be(HttpStatusCode.NoContent);
        approvals.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        HttpResponseMessage loser = approvals.Single(r => r.StatusCode != HttpStatusCode.NoContent);
        loser.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        approved.GetProperty("status").GetString().Should().Be("Approved");
        approved.GetProperty("history").EnumerateArray().Count(h => h.GetProperty("kind").GetString() == "Approved").Should().Be(1);
    }

    [Fact]
    public async Task Accepting_a_correction_reopens_the_timesheet_in_the_same_transaction()
    {
        (HttpClient eve, HttpClient sam, Guid eveId) = await TeamAsync();
        DateOnly week = PastMonday(3);
        await AddAsync(eve, week, 8, 12);
        await eve.PostAsync($"/api/timesheets/mine/{week:yyyy-MM-dd}/submit", null, Ct);
        Guid id = (await TimesheetAsync(eve, $"mine/{week:yyyy-MM-dd}")).GetProperty("id").GetGuid();

        HttpResponseMessage request = await eve.PostAsJsonAsync($"/api/timesheets/{id}/correction-requests", new { reason = "Wednesday shift is missing" }, Ct);
        HttpResponseMessage second = await eve.PostAsJsonAsync($"/api/timesheets/{id}/correction-requests", new { reason = "again" }, Ct);
        HttpResponseMessage blockedApproval = await sam.PostAsync($"/api/timesheets/{id}/approve", null, Ct);
        Guid requestId = (await JsonAsync(request)).GetProperty("id").GetGuid();
        HttpResponseMessage accept = await sam.PostAsJsonAsync($"/api/correction-requests/{requestId}/accept", new { comment = "go ahead" }, Ct);
        JsonElement reopened = await TimesheetAsync(sam, $"users/{eveId}/{week:yyyy-MM-dd}");

        request.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(second)).GetProperty("code").GetString().Should().Be("CorrectionRequestAlreadyPending");
        (await JsonAsync(blockedApproval)).GetProperty("code").GetString().Should().Be("PendingCorrectionRequest");
        accept.StatusCode.Should().Be(HttpStatusCode.NoContent);
        reopened.GetProperty("status").GetString().Should().Be("Draft");
        reopened.GetProperty("latestCorrection").GetProperty("decisionComment").GetString().Should().Be("go ahead");
        reopened.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("kind").GetString())
            .Should().EndWith(["CorrectionRequested", "Reopened"]);
    }

    [Fact]
    public async Task Rolls_back_the_request_when_the_timesheet_rejects_it()
    {
        (HttpClient eve, _, _) = await TeamAsync();
        DateOnly week = PastMonday(4);
        await AddAsync(eve, week, 8, 12);
        Guid id = (await TimesheetAsync(eve, $"mine/{week:yyyy-MM-dd}")).GetProperty("id").GetGuid();

        HttpResponseMessage onDraft = await eve.PostAsJsonAsync($"/api/timesheets/{id}/correction-requests", new { reason = "fix" }, Ct);
        await eve.PostAsync($"/api/timesheets/mine/{week:yyyy-MM-dd}/submit", null, Ct);
        HttpResponseMessage onSubmitted = await eve.PostAsJsonAsync($"/api/timesheets/{id}/correction-requests", new { reason = "fix" }, Ct);

        onDraft.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(onDraft)).GetProperty("code").GetString().Should().Be("TimesheetIsEditable");
        onSubmitted.StatusCode.Should().Be(HttpStatusCode.OK, "the rejected request was not left pending");
    }

    [Fact]
    public async Task Forbids_a_colleague_from_viewing_a_timesheet_and_validates_the_week()
    {
        (_, _, Guid eveId) = await TeamAsync();
        HttpClient ed = await SignedInAsync(await CreateAsync(Role.Employee));

        HttpResponseMessage colleague = await ed.GetAsync($"/api/timesheets/users/{eveId}/{PastMonday(1):yyyy-MM-dd}", Ct);
        HttpResponseMessage tuesday = await ed.GetAsync($"/api/timesheets/mine/{PastMonday(1).AddDays(1):yyyy-MM-dd}", Ct);

        colleague.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        tuesday.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Eve in a new team led by Sam, both signed in.</summary>
    private async Task<(HttpClient Eve, HttpClient Sam, Guid EveId)> TeamAsync()
    {
        User sam = await CreateAsync(Role.Supervisor);
        User eve = await CreateAsync(Role.Employee);
        using (var scopes = new Scopes(factory))
        {
            var team = Team.Create($"Team {Guid.CreateVersion7():N}", sam);
            await scopes.Get<ITeamRepository>().SaveOrUpdateAsync(team, Ct);
            IUserRepository usersRepository = scopes.Get<IUserRepository>();
            User saved = (await usersRepository.FindAsync(eve.Id, Ct))!;
            saved.MoveTo(team);
            await usersRepository.SaveOrUpdateAsync(saved, Ct);
        }

        return (await SignedInAsync(eve), await SignedInAsync(sam), eve.Id.Value);
    }

    private async Task<Guid> AddAsync(HttpClient client, DateOnly week, int fromHour, int toHour)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/time-entries", new { start = At(week, 1, fromHour), end = At(week, 1, toHour), note = "forgot my phone" }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> TimesheetAsync(HttpClient client, string path)
    {
        HttpResponseMessage response = await client.GetAsync($"/api/timesheets/{path}", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await JsonAsync(response);
    }

    private static int Version(JsonElement timesheet) => timesheet.GetProperty("version").GetInt32();

    /// <summary>The Monday <paramref name="weeksAgo"/> weeks before this week, in company time.</summary>
    private static DateOnly PastMonday(int weeksAgo)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(TimeProvider.System.GetUtcNow(), Bogota).DateTime);
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7) - (7 * weeksAgo));
    }

    private static DateTimeOffset At(DateOnly monday, int dayOffset, int hour) =>
        new DateTimeOffset(monday.AddDays(dayOffset).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.FromHours(-5));

    private Task<User> CreateAsync(Role role) => TestUsers.CreateActiveAsync(factory, role, Ct);

    private Task<HttpClient> SignedInAsync(User user) => TestUsers.SignedInClientAsync(factory, user, Ct);

    private async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
