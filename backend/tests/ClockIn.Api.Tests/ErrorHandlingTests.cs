using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Domain.Accounts;

namespace ClockIn.Api.Tests;

[Collection(SharedApi.Name)]
public sealed class ErrorHandlingTests(ApiFactory factory)
{
    [Theory]
    [InlineData("unauthorized", HttpStatusCode.Unauthorized, null)]
    [InlineData("not-found", HttpStatusCode.NotFound, null)]
    [InlineData("forbidden", HttpStatusCode.Forbidden, null)]
    [InlineData("conflict", HttpStatusCode.Conflict, "ConcurrencyConflict")]
    [InlineData("domain", HttpStatusCode.UnprocessableEntity, "EntryAlreadyOpen")]
    public async Task Maps_exceptions_to_problem_details_when_thrown(string kind, HttpStatusCode status, string? code)
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync($"/test/throw/{kind}", TestContext.Current.CancellationToken);
        JsonElement problem = await ReadProblemAsync(response);

        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        problem.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
        if (code is not null)
        {
            problem.GetProperty("code").GetString().Should().Be(code);
        }
    }

    [Fact]
    public async Task Returns_field_errors_when_validation_fails()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/test/throw/validation", TestContext.Current.CancellationToken);
        JsonElement problem = await ReadProblemAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.GetProperty("errors").GetProperty("note")[0].GetString().Should().Be("Note is required.");
    }

    [Fact]
    public async Task Hides_details_when_the_error_is_unexpected()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/test/throw/boom", TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContain("secret internal detail");
    }

    [Fact]
    public async Task Returns_problem_details_when_the_route_does_not_exist()
    {
        // Anonymous callers get 401 for every route, existing or not, so routes aren't revealed.
        User eve = await TestUsers.CreateActiveAsync(factory, Role.Employee, TestContext.Current.CancellationToken);
        HttpClient client = await TestUsers.SignedInClientAsync(factory, eve, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.GetAsync("/no-such-route", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
}
