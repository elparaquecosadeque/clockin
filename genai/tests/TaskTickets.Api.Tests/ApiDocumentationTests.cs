using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace TaskTickets.Api.Tests;

public sealed class ApiDocumentationTests
{
    [Fact]
    public async Task Development_documentation_has_bearer_security_and_prefilled_examples()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        var response = await client.GetAsync("/scalar");
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect);
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        var paths = document.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/auth/me").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/auth/login").GetProperty("post").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/auth/register").GetProperty("post").TryGetProperty("security", out _));
        var examples = paths.GetProperty("/api/auth/login").GetProperty("post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("examples");
        Assert.Equal("alice@example.test", examples.GetProperty("Alice").GetProperty("value").GetProperty("email").GetString());
        Assert.Equal("bob@example.test", examples.GetProperty("Bob").GetProperty("value").GetProperty("email").GetString());
        Assert.Equal("DemoPassword123!", examples.GetProperty("Alice").GetProperty("value").GetProperty("password").GetString());
        var registration = paths.GetProperty("/api/auth/register").GetProperty("post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("example");
        Assert.Equal("charlie@example.test", registration.GetProperty("email").GetString());
        Assert.True(paths.GetProperty("/api/task-tickets").GetProperty("post").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task Documentation_is_not_mapped_outside_development()
    {
        using var factory = new ApiFactory();
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = production.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "DemoPassword123!" });
        var token = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/scalar")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }
}
