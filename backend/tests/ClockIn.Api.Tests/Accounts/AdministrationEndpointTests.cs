using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Domain.Accounts;

namespace ClockIn.Api.Tests.Accounts;

/// <summary>The admin flows over HTTP against the real database: a team and a user from creation to archive.</summary>
[Collection(SharedApi.Name)]
public sealed class AdministrationEndpointTests(ApiFactory factory)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Runs_a_team_and_an_employee_through_their_lifecycle()
    {
        HttpClient asAda = await SignedInAsync(Role.Admin);
        User sam = await TestUsers.CreateActiveAsync(factory, Role.Supervisor, Ct);
        User sid = await TestUsers.CreateActiveAsync(factory, Role.Supervisor, Ct);
        string name = $"Team {Guid.CreateVersion7():N}";
        string email = $"fay-{Guid.CreateVersion7():N}@clockin.test";

        JsonElement team = await JsonAsync(await asAda.PostAsJsonAsync("/api/teams", new { name, supervisorId = sam.Id.Value }, Ct));
        Guid teamId = team.GetProperty("id").GetGuid();
        JsonElement link = await JsonAsync(await asAda.PostAsJsonAsync(
            "/api/users", new { email, name = "Fay", role = "Employee", teamId, language = "es", timeFormat = "12h" }, Ct));
        HttpResponseMessage activated = await TestUsers.HttpsClient(factory).PostAsJsonAsync(
            "/api/auth/set-password", new { token = link.GetProperty("token").GetString(), password = TestUsers.Password }, Ct);
        Guid fayId = (await UserAsync(asAda, email)).GetProperty("id").GetGuid();
        HttpResponseMessage renamed = await asAda.PutAsJsonAsync($"/api/teams/{teamId}/name", new { name = name.ToUpperInvariant() }, Ct);
        HttpResponseMessage handedOver = await asAda.PutAsJsonAsync($"/api/teams/{teamId}/supervisor", new { supervisorId = sid.Id.Value }, Ct);
        HttpResponseMessage busyTeam = await asAda.PostAsync($"/api/teams/{teamId}/archive", null, Ct);
        HttpResponseMessage promoted = await asAda.PutAsJsonAsync($"/api/users/{fayId}/role", new { role = "Supervisor" }, Ct);
        JsonElement fay = await UserAsync(asAda, email);
        HttpResponseMessage archived = await asAda.PostAsync($"/api/teams/{teamId}/archive", null, Ct);
        HttpResponseMessage deactivated = await asAda.PostAsync($"/api/users/{fayId}/deactivate", null, Ct);
        HttpResponseMessage reactivated = await asAda.PostAsJsonAsync($"/api/users/{fayId}/reactivate", new { }, Ct);
        JsonElement teams = await JsonAsync(await asAda.GetAsync("/api/teams", Ct));

        activated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        renamed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        handedOver.StatusCode.Should().Be(HttpStatusCode.NoContent);
        busyTeam.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        promoted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        fay.GetProperty("role").GetString().Should().Be("Supervisor");
        fay.GetProperty("teamId").ValueKind.Should().Be(JsonValueKind.Null);
        archived.StatusCode.Should().Be(HttpStatusCode.NoContent);
        deactivated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        reactivated.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement listed = teams.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == teamId);
        listed.GetProperty("name").GetString().Should().Be(name.ToUpperInvariant());
        listed.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        listed.GetProperty("supervisorId").GetGuid().Should().Be(sid.Id.Value);
    }

    [Fact]
    public async Task Locks_the_account_after_five_wrong_current_passwords()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);
        HttpClient client = await TestUsers.SignedInClientAsync(factory, eve, Ct);

        for (int i = 0; i < User.MaxFailedSignIns; i++)
        {
            HttpResponseMessage wrong = await client.PostAsJsonAsync(
                "/api/me/password", new { currentPassword = "not my password!", newPassword = "a brand new password" }, Ct);
            wrong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }

        HttpResponseMessage signIn = await TestUsers.HttpsClient(factory).PostAsJsonAsync(
            "/api/auth/login", new { email = eve.Email.Value, password = TestUsers.Password }, Ct);
        HttpResponseMessage oldSession = await client.GetAsync("/api/me", Ct);

        signIn.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        oldSession.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changes_preferences_and_password_for_the_signed_in_user()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);
        HttpClient phone = await TestUsers.SignedInClientAsync(factory, eve, Ct);
        HttpClient laptop = await TestUsers.SignedInClientAsync(factory, eve, Ct);

        HttpResponseMessage preferences = await phone.PutAsJsonAsync("/api/me/preferences", new { language = "es", timeFormat = "12h" }, Ct);
        JsonElement me = await JsonAsync(await phone.GetAsync("/api/me", Ct));
        HttpResponseMessage changed = await phone.PostAsJsonAsync(
            "/api/me/password", new { currentPassword = TestUsers.Password, newPassword = "a brand new password" }, Ct);
        phone.DefaultRequestHeaders.Authorization = new("Bearer", (await JsonAsync(changed)).GetProperty("accessToken").GetString());
        HttpResponseMessage laptopAfter = await laptop.GetAsync("/api/me", Ct);
        HttpResponseMessage phoneAfter = await phone.GetAsync("/api/me", Ct);
        HttpResponseMessage forbidden = await phone.GetAsync("/api/users", Ct);

        preferences.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (me.GetProperty("language").GetString(), me.GetProperty("timeFormat").GetString()).Should().Be(("es", "12h"));
        laptopAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        phoneAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> SignedInAsync(Role role) =>
        await TestUsers.SignedInClientAsync(factory, await TestUsers.CreateActiveAsync(factory, role, Ct), Ct);

    private async Task<JsonElement> UserAsync(HttpClient client, string email) =>
        (await JsonAsync(await client.GetAsync("/api/users", Ct))).EnumerateArray().Single(u => u.GetProperty("email").GetString() == email);

    private async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync(Ct);
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement;
    }
}
