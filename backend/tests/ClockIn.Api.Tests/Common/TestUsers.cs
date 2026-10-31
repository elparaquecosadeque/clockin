using System.Net.Http.Json;
using System.Text.Json;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Api.Tests.Common;

/// <summary>Creates users straight through the repositories, so API tests don't depend on the admin flows.</summary>
public static class TestUsers
{
    public const string Password = "correct horse battery";

    public static async Task<User> CreateActiveAsync(ApiFactory factory, Role role, CancellationToken ct)
    {
        using var scopes = new Scopes(factory);
        var user = User.Create(new Email($"{Guid.CreateVersion7():N}@clockin.test"), role.ToString(), role);
        user.SetPassword(scopes.Get<IPasswordHasher>().Hash(Password));
        await scopes.Get<IUserRepository>().SaveOrUpdateAsync(user, ct);
        return user;
    }

    /// <summary>A client over HTTPS (so the Secure refresh cookie round-trips) with a signed-in user's access token.</summary>
    public static async Task<HttpClient> SignedInClientAsync(ApiFactory factory, User user, CancellationToken ct)
    {
        HttpClient client = HttpsClient(factory);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email.Value, password = Password }, ct);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    public static HttpClient HttpsClient(ApiFactory factory) =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
}
