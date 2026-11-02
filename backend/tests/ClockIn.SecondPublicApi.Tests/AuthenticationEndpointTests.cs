using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Tests.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClockIn.SecondPublicApi.Tests;

public sealed class AuthenticationEndpointTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Public_hosts_reject_each_others_tokens()
    {
        await using var second = new SecondApiFactory();
        await second.PrepareAsync(Ct);
        await using var first = new FirstPublicApiFactory(second);
        using HttpClient firstClient = first.CreateClient();
        using HttpClient secondClient = second.CreateClient();
        string firstToken = await TokenAsync(firstClient);
        string secondToken = await TokenAsync(secondClient);
        firstClient.DefaultRequestHeaders.Authorization = new("Bearer", secondToken);
        secondClient.DefaultRequestHeaders.Authorization = new("Bearer", firstToken);
        (await firstClient.GetAsync("/api/v1/timesheets/mine/2026-10-26", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await secondClient.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        secondClient.DefaultRequestHeaders.Authorization = new("Bearer", secondToken);
        (await secondClient.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_expired_tokens_and_revoked_sessions(bool revoke)
    {
        await using var factory = new SecondApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient client = factory.CreateClient();
        string token = await TokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        if (revoke)
        {
            using IServiceScope scope = factory.Services.CreateScope();
            ISessionRepository sessionsRepository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            Session session = (await sessionsRepository.FindAsync(new SessionId(Guid.Parse(new JsonWebToken(token).GetClaim("sid").Value)), Ct))!;
            session.Revoke(factory.Clock.GetUtcNow());
            await sessionsRepository.SaveOrUpdateAsync(session, Ct);
        }
        else
        {
            factory.Clock.Set("2026-10-30 10:16");
        }

        (await client.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_and_activation_share_the_sign_in_rate_limit()
    {
        await using var factory = new SecondApiFactory();
        await factory.PrepareAsync(Ct);
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Auth:SignInPermitsPerMinute", "1"));
        using HttpClient client = limited.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "unknown@clockin.demo", password = "incorrect" }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync("/api/v1/auth/set-password", new { token = "invalid", password = "new-user-password" }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Validation_errors_include_fields_and_correlation_and_invalid_activation_is_rejected()
    {
        await using var factory = new SecondApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "second-api-test");
        HttpResponseMessage invalid = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = new string('a', 255), password = "incorrect" }, Ct);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("errors").TryGetProperty("email", out _).Should().BeTrue();
        body.RootElement.GetProperty("correlationId").GetString().Should().Be("second-api-test");
        (await client.PostAsJsonAsync("/api/v1/auth/set-password", new { token = "invalid", password = "new-user-password" }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync("/api/v1/auth/set-password", new { token = "invalid", password = "short" }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Public_info_and_documentation_do_not_create_or_prepare_a_database()
    {
        await using var factory = new SecondApiFactory();
        await using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("Demo:Enabled", "true");
        });
        using HttpClient client = configured.CreateClient();
        (await client.GetAsync("/api/v1/info", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/openapi/v1.json", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        File.Exists(new SqliteConnectionStringBuilder(factory.ConnectionString).DataSource).Should().BeFalse();
    }

    private async Task<string> TokenAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "mia@clockin.demo", password = "clockin-demo" }, Ct);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("expiresAt").GetDateTimeOffset().Should().Be(new CompanyClock().GetUtcNow().AddMinutes(15));
        body.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("accessToken", "expiresAt");
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }
}
