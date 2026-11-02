using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace ClockIn.SecondPublicApi.Tests;

public sealed class AccountEndpointTests
{
    private const string Password = "new-user-password";
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Public_info_is_anonymous_but_identity_requires_a_valid_token()
    {
        await using var factory = new SecondApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient client = factory.CreateClient();
        JsonElement info = await JsonAsync(await client.GetAsync("/api/v1/info", Ct));
        info.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("name", "version");
        info.GetProperty("name").GetString().Should().Be("ClockIn.SecondPublicApi");
        info.GetProperty("version").GetString().Should().Be("v1");
        (await client.GetAsync("/api/v1/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await SignInAsync(client, "mia@clockin.demo", "clockin-demo");
        JsonElement me = await JsonAsync(await client.GetAsync("/api/v1/me", Ct));
        me.GetProperty("email").GetString().Should().Be("mia@clockin.demo");
        me.GetProperty("permissions").GetArrayLength().Should().BeGreaterThan(0);
        (await client.PostAsJsonAsync("/api/v1/users", Supervisor(), Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("SQLite")]
    [InlineData("PostgreSQL")]
    public async Task Admin_creates_a_supervisor_who_must_activate_before_login(string provider)
    {
        await using var factory = new SecondApiFactory(provider: provider);
        await factory.PrepareAsync(Ct);
        using HttpClient admin = factory.CreateClient();
        (await admin.PostAsJsonAsync("/api/v1/users", Supervisor(), Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await SignInAsync(admin, "ada@clockin.demo", "clockin-demo");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/v1/users", Supervisor(), Ct);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        string activationToken = (await JsonAsync(created)).GetProperty("token").GetString()!;
        (await admin.PostAsJsonAsync("/api/v1/users", Supervisor(), Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using HttpClient user = factory.CreateClient();
        (await user.PostAsJsonAsync("/api/v1/auth/login", new { email = "new-supervisor@clockin.demo", password = Password }, Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var activation = new { token = activationToken, password = Password };
        (await user.PostAsJsonAsync("/api/v1/auth/set-password", activation, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await user.PostAsJsonAsync("/api/v1/auth/set-password", activation, Ct)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await SignInAsync(user, "new-supervisor@clockin.demo", Password);
        JsonElement me = await JsonAsync(await user.GetAsync("/api/v1/me", Ct));
        me.GetProperty("role").GetString().Should().Be("Supervisor");
        (await user.PostAsJsonAsync("/api/v1/users", Supervisor(), Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employee_creation_requires_a_team_and_the_Office_example_can_be_used()
    {
        await using var factory = new SecondApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient admin = factory.CreateClient();
        await SignInAsync(admin, "ada@clockin.demo", "clockin-demo");
        HttpResponseMessage invalid = await admin.PostAsJsonAsync("/api/v1/users", new { email = "employee@clockin.demo", name = "New Employee", role = "Employee" }, Ct);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(invalid)).GetProperty("errors").TryGetProperty("teamId", out _).Should().BeTrue();
        JsonElement document = await JsonAsync(await admin.GetAsync("/openapi/v1.json", Ct));
        JsonElement example = document.GetProperty("paths").GetProperty("/api/v1/users").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .GetProperty("examples").GetProperty("Employee").GetProperty("value");
        example.GetProperty("teamId").GetGuid().Should().NotBeEmpty();
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/v1/users", example, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        string token = (await JsonAsync(created)).GetProperty("token").GetString()!;
        using HttpClient employee = factory.CreateClient();
        (await employee.PostAsJsonAsync("/api/v1/auth/set-password", new { token, password = Password }, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await SignInAsync(employee, example.GetProperty("email").GetString()!, Password);
        (await JsonAsync(await employee.GetAsync("/api/v1/me", Ct))).GetProperty("teamName").GetString().Should().Be("Office");
    }

    [Fact]
    public async Task Scalar_contains_only_the_agreed_routes_and_safe_activation_examples()
    {
        await using var factory = new SecondApiFactory();
        await factory.PrepareAsync(Ct);
        using HttpClient client = factory.CreateClient();
        JsonElement document = await JsonAsync(await client.GetAsync("/openapi/v1.json", Ct));
        JsonElement paths = document.GetProperty("paths");
        paths.EnumerateObject().Should().HaveCount(5);
        JsonElement logins = paths.GetProperty("/api/v1/auth/login").GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("examples");
        logins.GetProperty("Ada").GetProperty("value").GetProperty("email").GetString().Should().Be("ada@clockin.demo");
        logins.GetProperty("Mia").GetProperty("value").GetProperty("password").GetString().Should().Be("clockin-demo");
        JsonElement activation = paths.GetProperty("/api/v1/auth/set-password").GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("examples").GetProperty("Activation").GetProperty("value");
        activation.GetProperty("password").GetString().Should().Be(Password);
        activation.GetProperty("token").GetString().Should().Be("paste-token-from-create-user-response");
        (await client.GetAsync("/scalar/v1", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Demo")]
    public void Refuses_startup_outside_development(string environment)
    {
        using var factory = new SecondApiFactory(environment);
        Action start = () => factory.CreateClient();
        start.Should().Throw<InvalidOperationException>().WithMessage("*Development*");
    }

    private static object Supervisor() => new { email = "new-supervisor@clockin.demo", name = "New Supervisor", role = "Supervisor" };

    private async Task SignInAsync(HttpClient client, string email, string password)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Ct);
        response.EnsureSuccessStatusCode();
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await JsonAsync(response)).GetProperty("accessToken").GetString());
    }

    private async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
