using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace TaskTickets.Api.Tests;

public sealed class AuthTests
{
    [Fact]
    public async Task Rejects_expired_tokens_using_a_controllable_clock()
    {
        var clock = new TestTimeProvider();
        using var factory = new ApiFactory();
        using var timed = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = timed.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "DemoPassword123!" });
        var token = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1800, token.GetProperty("expiresIn").GetInt32());
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        clock.Advance(TimeSpan.FromMinutes(31));
        var expired = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal("application/problem+json", expired.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("unknown_user")]
    [InlineData("missing_subject")]
    [InlineData("invalid_subject")]
    [InlineData("wrong_issuer")]
    [InlineData("wrong_audience")]
    [InlineData("future")]
    [InlineData("tampered")]
    [InlineData("unsigned")]
    public async Task Rejects_invalid_tokens(string kind)
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        var key = factory.Services.GetRequiredService<SymmetricSecurityKey>();
        var now = DateTime.UtcNow;
        var subject = kind == "unknown_user" ? Guid.NewGuid().ToString() : kind == "invalid_subject" ? "invalid" : "00000000-0000-4000-8000-000000000001";
        var jwt = new JwtSecurityToken(
            issuer: kind == "wrong_issuer" ? "Other" : "TaskTickets",
            audience: kind == "wrong_audience" ? "Other" : "TaskTickets.Client",
            claims: kind == "missing_subject" ? [] : [new Claim("sub", subject)],
            notBefore: kind == "future" ? now.AddMinutes(5) : now.AddMinutes(-1),
            expires: now.AddMinutes(30),
            signingCredentials: kind == "unsigned" ? null : new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        if (kind == "tampered")
        {
            var parts = token.Split('.');
            parts[2] = (parts[2][0] == 'a' ? "b" : "a") + parts[2][1..];
            token = string.Join('.', parts);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Fresh_host_reseeds_users_and_tickets_discards_registered_users_and_rejects_old_tokens()
    {
        using var first = new ApiFactory();
        using var alice = await first.LoginAsync();
        using var anonymous = first.NewClient();
        await anonymous.PostAsJsonAsync("/api/auth/register", new { displayName = "Charlie", email = "charlie@example.test", password = "DemoPassword123!" });
        var created = await alice.PostAsJsonAsync("/api/task-tickets", new { title = "Transient" });
        var seeded = await alice.GetFromJsonAsync<JsonElement>("/api/task-tickets");
        var seedId = seeded.GetProperty("items").EnumerateArray().First(item => item.GetProperty("title").GetString() == "Plan the demo").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/task-tickets/{seedId}")).StatusCode);

        using var second = new ApiFactory();
        using var stale = second.NewClient();
        stale.DefaultRequestHeaders.Authorization = alice.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Unauthorized, (await stale.GetAsync("/api/auth/me")).StatusCode);
        var login = await stale.PostAsJsonAsync("/api/auth/login", new { email = "charlie@example.test", password = "DemoPassword123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        using var freshAlice = await second.LoginAsync();
        using var freshBob = await second.LoginAsync("bob@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await freshAlice.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await freshAlice.GetAsync($"/api/task-tickets/{seedId}")).StatusCode);
        var page = await freshAlice.GetFromJsonAsync<JsonElement>("/api/task-tickets");
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("PerIp")]
    [InlineData("Global")]
    public async Task Authentication_rate_limit_returns_problem_details_and_retry_after(string limit)
    {
        using var factory = new ApiFactory();
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [$"AuthRateLimit:{limit}"] = "2" })));
        using var client = limited.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "DemoPassword123!" })).StatusCode);
        }
        var rejected = await client.PostAsJsonAsync("/api/auth/register", new { displayName = "Charlie", email = "charlie@example.test", password = "DemoPassword123!" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Registration_enforces_length_boundaries_without_trimming_passwords()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        var email = new string('a', 64) + "@" + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 57) + ".test";
        Assert.Equal(255, email.Length);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { displayName = "Name", email, password = "DemoPassword123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { displayName = new string('a', 101), email = "charlie@example.test", password = "DemoPassword123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { displayName = "Name", email = "charlie@example.test", password = new string('p', 129) })).StatusCode);
        var password = " " + new string('p', 126) + " ";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { displayName = " " + new string('a', 100) + " ", email = email[1..], password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = email[1..], password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = email[1..], password = password.Trim() })).StatusCode);
    }

    [Fact]
    public async Task Registration_trims_inputs_rejects_duplicate_and_does_not_issue_token()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new { displayName = " Charlie ", email = " charlie@example.test ", password = "DemoPassword123!" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Charlie", user.GetProperty("displayName").GetString());
        Assert.Equal("charlie@example.test", user.GetProperty("email").GetString());
        Assert.Equal(3, user.EnumerateObject().Count());

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { displayName = "Other", email = " CHARLIE@EXAMPLE.TEST ", password = "DemoPassword123!" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var charlie = await factory.LoginAsync("charlie@example.test");
        Assert.Equal(HttpStatusCode.OK, (await charlie.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("", "valid@example.test", "DemoPassword123!")]
    [InlineData("   ", "valid@example.test", "DemoPassword123!")]
    [InlineData(null, "valid@example.test", "DemoPassword123!")]
    [InlineData("Valid", "bad-email", "DemoPassword123!")]
    [InlineData("Valid", null, "DemoPassword123!")]
    [InlineData("Valid", "valid@example.test", "short")]
    [InlineData("Valid", "valid@example.test", null)]
    public async Task Rejects_invalid_registration(string? displayName, string? email, string? password)
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new { displayName, email, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Concurrent_normalized_duplicate_registration_inserts_exactly_one_user()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(index =>
            client.PostAsJsonAsync("/api/auth/register", new { displayName = "Charlie", email = index % 2 == 0 ? "charlie@example.test" : " CHARLIE@EXAMPLE.TEST ", password = "DemoPassword123!" })));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_return_identical_generic_failure()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        foreach (var email in new[] { "alice@example.test", "missing@example.test" })
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword123!" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Invalid email or password.", problem.GetProperty("title").GetString());
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Seeded_user_can_log_in_and_read_public_profile()
    {
        using var factory = new ApiFactory();
        using var client = await factory.LoginAsync();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("alice@example.test", user.GetProperty("email").GetString());
        Assert.Equal(3, user.EnumerateObject().Count());
        Assert.False(user.TryGetProperty("passwordHash", out _));
    }
}
