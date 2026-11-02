using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClockIn.PublicApi.Tests;

public sealed class AuthenticationEndpointTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sign_in_is_rate_limited_per_client()
    {
        await using var factory = new PublicApiFactory();
        await factory.PrepareAsync(Ct);
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Auth:SignInPermitsPerMinute", "1"));
        using HttpClient client = limited.CreateClient();
        var request = new { email = "unknown@clockin.demo", password = "incorrect" };
        (await client.PostAsJsonAsync("/api/v1/auth/login", request, Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync("/api/v1/auth/login", request, Ct)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Startup_never_creates_migrates_or_seeds_the_database()
    {
        await using var factory = new PublicApiFactory();
        await using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("Demo:Enabled", "true");
        });
        using HttpClient client = configured.CreateClient();
        (await client.GetAsync("/openapi/v1.json", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        string database = new SqliteConnectionStringBuilder(factory.ConnectionString).DataSource;
        File.Exists(database).Should().BeFalse();
    }

    [Fact]
    public async Task Hosts_share_accounts_and_changes_but_reject_each_others_tokens()
    {
        await using var publicApi = new PublicApiFactory();
        await publicApi.PrepareAsync(Ct);
        await using var spaApi = new SharedDatabaseApiFactory(publicApi);
        using HttpClient publicClient = publicApi.CreateClient();
        using HttpClient spaClient = spaApi.CreateClient();
        string publicToken = await TokenAsync(publicClient, "/api/v1/auth/login");
        string spaToken = await TokenAsync(spaClient, "/api/auth/login");
        publicClient.DefaultRequestHeaders.Authorization = new("Bearer", spaToken);
        spaClient.DefaultRequestHeaders.Authorization = new("Bearer", publicToken);
        (await publicClient.GetAsync("/api/v1/timesheets/mine/2026-10-26", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await spaClient.GetAsync("/api/timesheets/mine/2026-10-26", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        publicClient.DefaultRequestHeaders.Authorization = new("Bearer", publicToken);
        spaClient.DefaultRequestHeaders.Authorization = new("Bearer", spaToken);
        (await publicClient.PostAsync("/api/v1/time-entries/clock-in", null, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage sharedWeek = await spaClient.GetAsync("/api/timesheets/mine/2026-10-26", Ct);
        sharedWeek.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await sharedWeek.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("entries").GetArrayLength().Should().BeGreaterThan(0);
        publicApi.Clock.Set("2026-10-30 10:01");
        (await spaClient.PostAsync("/api/time-entries/clock-out", null, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await publicClient.PostAsync("/api/v1/time-entries/clock-out", null, Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_expired_tokens_and_revoked_sessions(bool revoke)
    {
        await using var factory = new PublicApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient client = factory.CreateClient();
        string token = await TokenAsync(client, "/api/v1/auth/login");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        if (revoke)
        {
            var jwt = new JsonWebToken(token);
            using IServiceScope scope = factory.Services.CreateScope();
            ISessionRepository sessionsRepository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            Session session = (await sessionsRepository.FindAsync(new SessionId(Guid.Parse(jwt.GetClaim("sid").Value)), Ct))!;
            session.Revoke(factory.Clock.GetUtcNow());
            await sessionsRepository.SaveOrUpdateAsync(session, Ct);
        }
        else
        {
            factory.Clock.Set("2026-10-30 10:16");
        }

        (await client.GetAsync("/api/v1/timesheets/mine/2026-10-26", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Failed_login_counters_survive_rejected_requests()
    {
        await using var factory = new PublicApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "mia@clockin.demo", password = "incorrect" }, Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using IServiceScope scope = factory.Services.CreateScope();
        User user = (await scope.ServiceProvider.GetRequiredService<IUserRepository>().FindByEmailAsync(new Email("mia@clockin.demo"), Ct))!;
        user.FailedSignIns.Should().Be(1);
    }

    private async Task<string> TokenAsync(HttpClient client, string route)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(route, new { email = "mia@clockin.demo", password = "clockin-demo" }, Ct);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }
}
