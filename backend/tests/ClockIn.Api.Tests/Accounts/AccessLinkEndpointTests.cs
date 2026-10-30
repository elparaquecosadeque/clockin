using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Api.Tests.Accounts;

/// <summary>seed-first-admin, generate-access-link and set-password-with-link over HTTP.</summary>
[Collection(SharedApi.Name)]
public sealed class AccessLinkEndpointTests(ApiFactory factory)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeds_the_first_admin_from_the_deploy_config_on_startup()
    {
        using var scopes = new Scopes(factory);

        User? admin = await scopes.Get<IUserRepository>().FindByEmailAsync(new Email(ApiFactory.FirstAdminEmail), Ct);

        admin.Should().NotBeNull();
        admin!.Role.Should().Be(Role.Admin);
    }

    [Fact]
    public async Task Resets_a_password_with_a_link_an_admin_generated()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);
        HttpClient eveBefore = await TestUsers.SignedInClientAsync(factory, eve, Ct);
        HttpClient ada = await TestUsers.SignedInClientAsync(factory, await TestUsers.CreateActiveAsync(factory, Role.Admin, Ct), Ct);

        HttpResponseMessage generated = await ada.PostAsync($"/api/users/{eve.Id.Value}/access-links", null, Ct);
        string token = (await JsonAsync(generated)).GetProperty("token").GetString()!;
        HttpResponseMessage set = await SetPasswordAsync(token, "a brand new passphrase");
        HttpResponseMessage signIn = await TestUsers.HttpsClient(factory)
            .PostAsJsonAsync("/api/auth/login", new { email = eve.Email.Value, password = "a brand new passphrase" }, Ct);

        generated.StatusCode.Should().Be(HttpStatusCode.OK);
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);
        signIn.StatusCode.Should().Be(HttpStatusCode.OK);
        (await eveBefore.GetAsync("/api/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "earlier sessions end");
    }

    [Fact]
    public async Task Forbids_non_admins_from_generating_links()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);
        HttpClient sam = await TestUsers.SignedInClientAsync(factory, await TestUsers.CreateActiveAsync(factory, Role.Supervisor, Ct), Ct);

        HttpResponseMessage response = await sam.PostAsync($"/api/users/{eve.Id.Value}/access-links", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rejects_an_unusable_link_with_a_generic_rule()
    {
        HttpResponseMessage response = await SetPasswordAsync("0193a1b2c3d47e8f9a0b1c2d3e4f5a6b.nope", "a brand new passphrase");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("InvalidOrExpiredLink");
    }

    [Fact]
    public async Task Rejects_a_password_shorter_than_12_characters()
    {
        HttpResponseMessage response = await SetPasswordAsync("0193a1b2c3d47e8f9a0b1c2d3e4f5a6b.nope", "11 chars...");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("password", out _).Should().BeTrue();
    }

    private Task<HttpResponseMessage> SetPasswordAsync(string token, string password) =>
        TestUsers.HttpsClient(factory).PostAsJsonAsync("/api/auth/set-password", new { token, password }, Ct);

    private async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
