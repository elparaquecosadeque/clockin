using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Domain.Accounts;

namespace ClockIn.Api.Tests.Timesheets;

/// <summary>clock-in.feature and clock-out.feature over HTTP: the happy path and permissions.</summary>
[Collection(SharedApi.Name)]
public sealed class ClockEndpointTests(ApiFactory factory)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Opens_then_closes_an_entry_when_an_employee_punches_in_and_out()
    {
        HttpClient eve = await SignedInAsync(Role.Employee);

        HttpResponseMessage clockIn = await eve.PostAsync("/api/time-entries/clock-in", null, Ct);
        HttpResponseMessage clockOut = await eve.PostAsync("/api/time-entries/clock-out", null, Ct);

        clockIn.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement opened = await JsonAsync(clockIn);
        opened.GetProperty("end").ValueKind.Should().Be(JsonValueKind.Null);
        clockOut.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement closed = await JsonAsync(clockOut);
        closed.GetProperty("id").GetGuid().Should().Be(opened.GetProperty("id").GetGuid());
        closed.GetProperty("end").GetDateTimeOffset().Should().BeAfter(closed.GetProperty("start").GetDateTimeOffset());
    }

    [Fact]
    public async Task Lets_a_supervisor_clock_in()
    {
        HttpClient sam = await SignedInAsync(Role.Supervisor);

        HttpResponseMessage response = await sam.PostAsync("/api/time-entries/clock-in", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Forbids_admins()
    {
        HttpClient ada = await SignedInAsync(Role.Admin);

        HttpResponseMessage response = await ada.PostAsync("/api/time-entries/clock-in", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rejects_a_second_clock_in_while_an_entry_is_open()
    {
        HttpClient eve = await SignedInAsync(Role.Employee);
        await eve.PostAsync("/api/time-entries/clock-in", null, Ct);

        HttpResponseMessage response = await eve.PostAsync("/api/time-entries/clock-in", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("EntryAlreadyOpen");
    }

    [Fact]
    public async Task Opens_only_one_entry_when_two_devices_clock_in_at_the_same_moment()
    {
        HttpClient eve = await SignedInAsync(Role.Employee);

        HttpResponseMessage[] responses = await Task.WhenAll(
            eve.PostAsync("/api/time-entries/clock-in", null, Ct),
            eve.PostAsync("/api/time-entries/clock-in", null, Ct));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        HttpResponseMessage loser = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
        loser.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(loser)).GetProperty("code").GetString().Should().Be("EntryAlreadyOpen");
    }

    [Fact]
    public async Task Rejects_a_clock_out_when_no_entry_is_open()
    {
        HttpClient eve = await SignedInAsync(Role.Employee);

        HttpResponseMessage response = await eve.PostAsync("/api/time-entries/clock-out", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("NoOpenEntry");
    }

    private async Task<HttpClient> SignedInAsync(Role role) =>
        await TestUsers.SignedInClientAsync(factory, await TestUsers.CreateActiveAsync(factory, role, Ct), Ct);

    private async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
