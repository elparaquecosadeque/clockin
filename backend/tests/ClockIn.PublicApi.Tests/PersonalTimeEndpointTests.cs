using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Tests.Common;

namespace ClockIn.PublicApi.Tests;

/// <summary>personal-time.feature: real HTTP, handlers, transactions and development database.</summary>
public sealed class PersonalTimeEndpointTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Scalar_prefills_Mias_week_entry_and_latest_version()
    {
        await using var factory = new PublicApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient client = await SignInAsync(factory, "mia", Ct);
        JsonElement sheet = await JsonAsync(await client.GetAsync("/api/v1/timesheets/mine/2026-10-19", Ct));
        JsonElement document = await JsonAsync(await client.GetAsync("/openapi/v1.json", Ct));
        JsonElement paths = document.GetProperty("paths");
        foreach (string route in new[] { "/api/v1/timesheets/mine/{weekStart}", "/api/v1/timesheets/mine/{weekStart}/submit" })
        {
            JsonElement operation = paths.GetProperty(route).EnumerateObject().Single().Value;
            operation.GetProperty("parameters").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "weekStart")
                .GetProperty("example").GetString().Should().Be("2026-10-19");
        }

        JsonElement editOperation = paths.GetProperty("/api/v1/time-entries/{entryId}").GetProperty("put");
        Guid entryId = editOperation.GetProperty("parameters").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "entryId")
            .GetProperty("example").GetGuid();
        JsonElement entry = sheet.GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == entryId);
        JsonElement edit = editOperation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .GetProperty("examples").GetProperty("Mia").GetProperty("value");
        edit.GetProperty("version").GetInt32().Should().Be(sheet.GetProperty("version").GetInt32());
        edit.GetProperty("start").GetDateTimeOffset().Should().Be(entry.GetProperty("start").GetDateTimeOffset());
        edit.GetProperty("end").GetDateTimeOffset().Should().Be(entry.GetProperty("end").GetDateTimeOffset());
        (await client.PutAsJsonAsync($"/api/v1/time-entries/{entryId}", edit, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement refreshed = await JsonAsync(await client.GetAsync("/openapi/v1.json", Ct));
        refreshed.GetProperty("paths").GetProperty("/api/v1/time-entries/{entryId}").GetProperty("put")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .GetProperty("examples").GetProperty("Mia").GetProperty("value").GetProperty("version").GetInt32()
            .Should().BeGreaterThan(edit.GetProperty("version").GetInt32());
    }

    [Theory]
    [InlineData("SQLite")]
    [InlineData("PostgreSQL")]
    public async Task Punches_edits_with_concurrency_and_preserves_history(string provider)
    {
        await using var factory = new PublicApiFactory(provider: provider);
        await factory.PrepareAsync(Ct);
        using HttpClient client = await SignInAsync(factory, "mia", Ct);
        JsonElement opened = await JsonAsync(await client.PostAsync("/api/v1/time-entries/clock-in", null, Ct));
        factory.Clock.Set("2026-10-30 10:10");
        HttpResponseMessage closed = await client.PostAsync("/api/v1/time-entries/clock-out", null, Ct);
        closed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(closed)).GetProperty("id").GetGuid().Should().Be(opened.GetProperty("id").GetGuid());
        JsonElement sheet = await JsonAsync(await client.GetAsync("/api/v1/timesheets/mine/2026-10-26", Ct));
        Guid entryId = opened.GetProperty("id").GetGuid();
        var edit = new { start = CompanyClock.At("2026-10-30 10:01"), end = CompanyClock.At("2026-10-30 10:09"), note = "Corrected shift", version = sheet.GetProperty("version").GetInt32() };
        (await client.PutAsJsonAsync($"/api/v1/time-entries/{entryId}", edit, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync($"/api/v1/time-entries/{entryId}", edit, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        JsonElement updated = await JsonAsync(await client.GetAsync("/api/v1/timesheets/mine/2026-10-26", Ct));
        updated.GetProperty("history").GetArrayLength().Should().BeGreaterThan(0);
        JsonElement entry = updated.GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == entryId);
        entry.GetProperty("punchedStart").GetDateTimeOffset().Should().Be(CompanyClock.At("2026-10-30 10:00"));
        entry.GetProperty("note").GetString().Should().Be("Corrected shift");
    }

    [Fact]
    public async Task Submits_an_ended_week_but_rejects_current_week_and_invalid_week_dates()
    {
        await using var factory = new PublicApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient client = await SignInAsync(factory, "mia", Ct);
        (await client.PostAsync("/api/v1/timesheets/mine/2026-10-26/submit", null, Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsync("/api/v1/timesheets/mine/2026-10-20/submit", null, Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/v1/timesheets/mine/2026-10-20", Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsync("/api/v1/timesheets/mine/2026-10-19/submit", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement sheet = await JsonAsync(await client.GetAsync("/api/v1/timesheets/mine/2026-10-19", Ct));
        sheet.GetProperty("status").GetString().Should().Be("Submitted");
        JsonElement entry = sheet.GetProperty("entries")[0];
        (await client.PutAsJsonAsync($"/api/v1/time-entries/{entry.GetProperty("id").GetGuid()}", new
        {
            start = entry.GetProperty("start").GetDateTimeOffset(),
            end = entry.GetProperty("end").GetDateTimeOffset(),
            note = "Locked week",
            version = sheet.GetProperty("version").GetInt32(),
        }, Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Enforces_authentication_permissions_ownership_and_field_validation()
    {
        await using var factory = new PublicApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient anonymous = factory.CreateClient();
        (await anonymous.PostAsync("/api/v1/time-entries/clock-in", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using HttpClient mia = await SignInAsync(factory, "mia", Ct);
        using HttpClient ada = await SignInAsync(factory, "ada", Ct);
        using HttpClient sam = await SignInAsync(factory, "sam", Ct);
        (await ada.PostAsync("/api/v1/time-entries/clock-in", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        JsonElement sheet = await JsonAsync(await mia.GetAsync("/api/v1/timesheets/mine/2026-10-19", Ct));
        JsonElement entry = sheet.GetProperty("entries")[0];
        string route = $"/api/v1/time-entries/{entry.GetProperty("id").GetGuid()}";
        var edit = new { start = entry.GetProperty("start").GetDateTimeOffset(), end = entry.GetProperty("end").GetDateTimeOffset(), note = "Another user", version = sheet.GetProperty("version").GetInt32() };
        (await sam.PutAsJsonAsync(route, edit, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ada.PutAsJsonAsync(route, edit, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        HttpResponseMessage invalid = await mia.PutAsJsonAsync(route, edit with { note = "" }, Ct);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(invalid)).GetProperty("errors").TryGetProperty("note", out _).Should().BeTrue();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = new string('a', 255), password = "bad" }, Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_returns_only_short_lived_access_token_and_docs_describe_bearer_auth()
    {
        await using var factory = new PublicApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "mia@clockin.demo", password = "clockin-demo" }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
        JsonElement body = await JsonAsync(response);
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("accessToken", "expiresAt");
        body.GetProperty("expiresAt").GetDateTimeOffset().Should().Be(factory.Clock.GetUtcNow().AddMinutes(15));
        JsonElement document = await JsonAsync(await client.GetAsync("/openapi/v1.json", Ct));
        document.GetProperty("paths").EnumerateObject().Should().HaveCount(6);
        document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString().Should().Be("bearer");
        JsonElement examples = document.GetProperty("paths").GetProperty("/api/v1/auth/login").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("examples");
        examples.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("Mia", "Sam", "Ada");
        examples.GetProperty("Mia").GetProperty("value").GetProperty("email").GetString().Should().Be("mia@clockin.demo");
        examples.GetProperty("Mia").GetProperty("value").GetProperty("password").GetString().Should().Be("clockin-demo");
        (await client.GetAsync("/scalar/v1", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Demo")]
    public void Refuses_to_start_outside_development(string environment)
    {
        using var factory = new PublicApiFactory(environment);
        Action start = () => factory.CreateClient();
        start.Should().Throw<InvalidOperationException>().WithMessage("*Development*");
    }

    private static async Task<HttpClient> SignInAsync(PublicApiFactory factory, string name, CancellationToken ct)
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = $"{name}@clockin.demo", password = "clockin-demo" }, ct);
        response.EnsureSuccessStatusCode();
        JsonElement body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement;
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
