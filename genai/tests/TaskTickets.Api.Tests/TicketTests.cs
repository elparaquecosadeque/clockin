using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskTickets.Api.Tests;

public sealed class TicketTests
{
    private const string BobId = "00000000-0000-4000-8000-000000000002";

    [Fact]
    public async Task Anonymous_requests_cannot_access_any_ticket_operation()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        const string ticket = "/api/task-tickets/00000000-0000-4000-8000-000000000101";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/task-tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(ticket)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/task-tickets", new { title = "Unauthorized" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(ticket, new { title = "Unauthorized", status = "done" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"{ticket}/assignee", new { assignedToUserId = BobId })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync(ticket)).StatusCode);
    }

    [Fact]
    public async Task Racing_reassignment_and_edit_never_restores_a_previous_assignees_access()
    {
        using var factory = new ApiFactory();
        using var alice = await factory.LoginAsync();
        using var bob = await factory.LoginAsync("bob@example.test");
        const string aliceId = "00000000-0000-4000-8000-000000000001";
        var created = await alice.PostAsJsonAsync("/api/task-tickets", new { title = "Race", assignedToUserId = BobId });
        var location = created.Headers.Location!;
        for (var index = 0; index < 10; index++)
        {
            Assert.Equal(HttpStatusCode.OK, (await alice.PutAsJsonAsync($"{location}/assignee", new { assignedToUserId = BobId })).StatusCode);
            var responses = await Task.WhenAll(
                bob.PutAsJsonAsync(location, new { title = "Bob's edit", status = "done" }),
                alice.PutAsJsonAsync($"{location}/assignee", new { assignedToUserId = aliceId }));
            Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
            Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
            var ticket = await alice.GetFromJsonAsync<JsonElement>(location);
            Assert.Equal(aliceId, ticket.GetProperty("assignedToUserId").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync(location, new { title = "Stale edit", status = "todo" })).StatusCode);
            var bobList = await bob.GetFromJsonAsync<JsonElement>("/api/task-tickets");
            Assert.DoesNotContain(bobList.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == ticket.GetProperty("id").GetGuid());
        }
    }

    [Fact]
    public async Task Seeded_lists_show_only_created_or_assigned_tickets_with_filtering_and_pagination()
    {
        using var factory = new ApiFactory();
        using var alice = await factory.LoginAsync();
        using var bob = await factory.LoginAsync("bob@example.test");
        var alicePage = await alice.GetFromJsonAsync<JsonElement>("/api/task-tickets?pageSize=1");
        Assert.Equal(2, alicePage.GetProperty("totalCount").GetInt32());
        Assert.Single(alicePage.GetProperty("items").EnumerateArray());
        Assert.Equal("in_progress", alicePage.GetProperty("items")[0].GetProperty("status").GetString());
        var bobPage = await bob.GetFromJsonAsync<JsonElement>("/api/task-tickets");
        Assert.Equal(2, bobPage.GetProperty("totalCount").GetInt32());
        Assert.Equal("done", bobPage.GetProperty("items")[0].GetProperty("status").GetString());
        var filtered = await alice.GetFromJsonAsync<JsonElement>("/api/task-tickets?status=todo");
        Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
        var beyond = await alice.GetFromJsonAsync<JsonElement>("/api/task-tickets?page=2147483647&pageSize=100");
        Assert.Empty(beyond.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Assignee_can_edit_but_only_creator_can_reassign_or_delete_and_previous_assignee_loses_access()
    {
        using var factory = new ApiFactory();
        using var alice = await factory.LoginAsync();
        using var bob = await factory.LoginAsync("bob@example.test");
        using var anonymous = factory.NewClient();
        await anonymous.PostAsJsonAsync("/api/auth/register", new { displayName = "Charlie", email = "charlie@example.test", password = "DemoPassword123!" });
        using var charlie = await factory.LoginAsync("charlie@example.test");
        var response = await alice.PostAsJsonAsync("/api/task-tickets", new { title = "Shared", assignedToUserId = BobId });
        var location = response.Headers.Location!;
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bob.PutAsJsonAsync(location, new { title = "Updated by Bob", status = "in_progress" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.DeleteAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PutAsJsonAsync($"{location}/assignee", new { assignedToUserId = BobId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await charlie.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await charlie.PutAsJsonAsync(location, new { title = "Intrusion", status = "done" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await charlie.DeleteAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await charlie.PutAsJsonAsync($"{location}/assignee", new { assignedToUserId = BobId })).StatusCode);

        var profile = await charlie.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var reassigned = await alice.PutAsJsonAsync($"{location}/assignee", new { assignedToUserId = profile.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.OK, reassigned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync(location, new { title = "Stale update", status = "done" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await charlie.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync(location)).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"title\":null}")]
    [InlineData("{\"title\":\"   \"}")]
    [InlineData("{\"title\":\"Test\",\"status\":\"done\"}")]
    [InlineData("{\"title\":\"Test\",\"dueDate\":\"2026-02-30\"}")]
    [InlineData("{\"title\":\"Test\",\"assignedToUserId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task Rejects_invalid_ticket_creation(string json)
    {
        using var factory = new ApiFactory();
        using var client = await factory.LoginAsync();
        var response = await client.PostAsync("/api/task-tickets", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=invalid")]
    [InlineData("status=0")]
    [InlineData("page=no")]
    public async Task Rejects_invalid_list_parameters(string query)
    {
        using var factory = new ApiFactory();
        using var client = await factory.LoginAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/task-tickets?{query}")).StatusCode);
    }

    [Fact]
    public async Task Enforces_ticket_length_limits_and_requires_string_status_on_update()
    {
        using var factory = new ApiFactory();
        using var client = await factory.LoginAsync();
        var created = await client.PostAsJsonAsync("/api/task-tickets", new { title = " " + new string('a', 200) + " ", description = new string('d', 5000) });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/task-tickets", new { title = new string('a', 201) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/task-tickets", new { title = "Test", description = new string('d', 5001) })).StatusCode);
        var location = created.Headers.Location!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(location, new { title = "Missing status" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(location, new { title = "Numeric status", status = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(location, new { title = "Unknown status", status = "blocked" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{location}/assignee", new { assignedToUserId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{location}/assignee", new { })).StatusCode);
        foreach (var status in new[] { "done", "todo", "in_progress" })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(location, new { title = "Any transition", status })).StatusCode);
        }
    }

    [Fact]
    public async Task Concurrent_edits_are_complete_and_last_completed_write_wins()
    {
        using var factory = new ApiFactory();
        using var alice = await factory.LoginAsync();
        using var bob = await factory.LoginAsync("bob@example.test");
        var created = await alice.PostAsJsonAsync("/api/task-tickets", new { title = "Shared", assignedToUserId = BobId });
        var location = created.Headers.Location!;
        var updates = await Task.WhenAll(
            alice.PutAsJsonAsync(location, new { title = "Alice", description = "Alice", status = "done" }),
            bob.PutAsJsonAsync(location, new { title = "Bob", description = "Bob", status = "in_progress" }));
        Assert.All(updates, result => Assert.Equal(HttpStatusCode.OK, result.StatusCode));
        var ticket = await alice.GetFromJsonAsync<JsonElement>(location);
        var title = ticket.GetProperty("title").GetString();
        Assert.Contains(title, new[] { "Alice", "Bob" });
        Assert.Equal(title, ticket.GetProperty("description").GetString());
        Assert.Equal(title == "Alice" ? "done" : "in_progress", ticket.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.OK, (await bob.PutAsJsonAsync(location, new { title = "Final", status = "todo" })).StatusCode);
        ticket = await alice.GetFromJsonAsync<JsonElement>(location);
        Assert.Equal("Final", ticket.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Creator_can_create_read_replace_and_delete_a_ticket()
    {
        using var factory = new ApiFactory();
        using var client = await factory.LoginAsync();
        var response = await client.PostAsJsonAsync("/api/task-tickets", new { title = " First ticket ", description = "Example", dueDate = "2020-01-01" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("First ticket", created.GetProperty("title").GetString());
        Assert.Equal("todo", created.GetProperty("status").GetString());
        Assert.Equal(created.GetProperty("createdByUserId").GetGuid(), created.GetProperty("assignedToUserId").GetGuid());
        Assert.Equal("2020-01-01", created.GetProperty("dueDate").GetString());
        var location = response.Headers.Location;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(location)).StatusCode);

        var updated = await client.PutAsJsonAsync(location, new { title = "Changed", status = "done" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var ticket = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("done", ticket.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, ticket.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, ticket.GetProperty("dueDate").ValueKind);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(location)).StatusCode);
    }
}
