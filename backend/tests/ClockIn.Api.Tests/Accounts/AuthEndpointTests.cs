using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClockIn.Api.Tests.Accounts;

/// <summary>log-in, refresh-session and log-out over HTTP: tokens, the refresh cookie and per-request checks.</summary>
[Collection(SharedApi.Name)]
public sealed class AuthEndpointTests(ApiFactory factory)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Issues_a_15_minute_access_token_and_a_locked_down_refresh_cookie_when_signing_in()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);

        HttpResponseMessage response = await LogInAsync(TestUsers.HttpsClient(factory), eve.Email.Value, TestUsers.Password);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await JsonAsync(response);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("expiresAt").GetDateTimeOffset().Should().BeCloseTo(TimeProvider.System.GetUtcNow().AddMinutes(15), TimeSpan.FromMinutes(1));
        string cookie = response.Headers.GetValues("Set-Cookie").Single();
        cookie.Should().StartWith("refresh_token=")
            .And.ContainEquivalentOf("path=/api/auth")
            .And.ContainEquivalentOf("secure")
            .And.ContainEquivalentOf("samesite=strict")
            .And.ContainEquivalentOf("httponly");
    }

    [Fact]
    public async Task Rejects_bad_credentials_with_a_generic_rule()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);

        HttpResponseMessage response = await LogInAsync(TestUsers.HttpsClient(factory), eve.Email.Value, "wrong password!!");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("InvalidCredentials");
    }

    [Fact]
    public async Task Returns_the_signed_in_user_and_permissions_from_me()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);
        HttpClient client = await TestUsers.SignedInClientAsync(factory, eve, Ct);

        JsonElement me = await JsonAsync(await client.GetAsync("/api/me", Ct));

        me.GetProperty("id").GetGuid().Should().Be(eve.Id.Value);
        me.GetProperty("role").GetString().Should().Be("Employee");
        me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Contain(Permissions.PunchTime);
    }

    [Fact]
    public async Task Rejects_a_request_with_no_access_token()
    {
        HttpResponseMessage response = await TestUsers.HttpsClient(factory).GetAsync("/api/me", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rejects_an_access_token_once_the_password_was_reset()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);
        HttpClient client = await TestUsers.SignedInClientAsync(factory, eve, Ct);
        await ResetPasswordAsync(eve);

        HttpResponseMessage response = await client.GetAsync("/api/me", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Issues_new_tokens_when_refreshing_with_the_cookie()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);
        HttpClient client = await TestUsers.SignedInClientAsync(factory, eve, Ct);

        HttpResponseMessage response = await client.PostAsync("/api/auth/refresh", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        response.Headers.GetValues("Set-Cookie").Single().Should().StartWith("refresh_token=");
    }

    [Fact]
    public async Task Rejects_a_refresh_with_no_cookie()
    {
        HttpResponseMessage response = await TestUsers.HttpsClient(factory).PostAsync("/api/auth/refresh", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ends_the_session_and_clears_the_cookie_when_logging_out()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);
        HttpClient client = await TestUsers.SignedInClientAsync(factory, eve, Ct);

        HttpResponseMessage logout = await client.PostAsync("/api/auth/logout", null, Ct);
        HttpResponseMessage refresh = await client.PostAsync("/api/auth/refresh", null, Ct);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        logout.Headers.GetValues("Set-Cookie").Single().Should().ContainEquivalentOf("expires=Thu, 01 Jan 1970");
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rejects_the_access_token_of_a_logged_out_session_only()
    {
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, Ct);
        HttpClient phone = await TestUsers.SignedInClientAsync(factory, eve, Ct);
        HttpClient laptop = await TestUsers.SignedInClientAsync(factory, eve, Ct);

        await phone.PostAsync("/api/auth/logout", null, Ct);

        (await phone.GetAsync("/api/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await laptop.GetAsync("/api/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rate_limits_sign_in_attempts_per_client()
    {
        using WebApplicationFactory<Program> strict = factory.WithWebHostBuilder(b => b.UseSetting("Auth:SignInPermitsPerMinute", "2"));
        HttpClient client = strict.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        await LogInAsync(client, "nobody@clockin.test", "whatever password");
        await LogInAsync(client, "nobody@clockin.test", "whatever password");
        HttpResponseMessage third = await LogInAsync(client, "nobody@clockin.test", "whatever password");

        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private Task<HttpResponseMessage> LogInAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, Ct);

    private async Task ResetPasswordAsync(User user)
    {
        using var scopes = new Scopes(factory);
        User loaded = (await scopes.Get<IUserRepository>().FindAsync(user.Id, Ct))!;
        loaded.SetPassword(scopes.Get<IPasswordHasher>().Hash("a brand new passphrase"));
        await scopes.Get<IUserRepository>().SaveOrUpdateAsync(loaded, Ct);
    }

    private async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
