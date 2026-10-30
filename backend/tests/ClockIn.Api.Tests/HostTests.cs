using System.Net;
using AwesomeAssertions;
using ClockIn.Api.Common;
using ClockIn.Api.Tests.Common;

namespace ClockIn.Api.Tests;

[Collection(SharedApi.Name)]
public sealed class HostTests(ApiFactory factory)
{
    [Fact]
    public async Task Reports_live_when_the_process_is_up()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reports_ready_when_the_database_is_reachable()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Echoes_the_correlation_id_when_the_client_sends_one()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.Header, "abc-123");

        HttpResponseMessage response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.Headers.GetValues(CorrelationIdMiddleware.Header).Should().ContainSingle().Which.Should().Be("abc-123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("contains spaces and <script>")]
    public async Task Generates_a_correlation_id_when_the_header_is_missing_or_unsafe(string? incoming)
    {
        HttpClient client = factory.CreateClient();
        if (incoming is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(CorrelationIdMiddleware.Header, incoming);
        }

        HttpResponseMessage response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        string generated = response.Headers.GetValues(CorrelationIdMiddleware.Header).Single();
        generated.Should().MatchRegex("^[0-9a-f]{32}$");
    }
}
