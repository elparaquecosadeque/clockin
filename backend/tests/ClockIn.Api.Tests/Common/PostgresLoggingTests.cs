using System.Diagnostics;
using System.Globalization;
using AwesomeAssertions;
using ClockIn.Api.Logging;
using Npgsql;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Testcontainers.PostgreSql;

namespace ClockIn.Api.Tests.Common;

public sealed class PostgresLoggingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            CREATE TABLE clockin_logs (
                timestamp timestamptz NOT NULL, level text NOT NULL, message text NOT NULL,
                message_template text NOT NULL, exception text, properties jsonb NOT NULL,
                service text NOT NULL, environment text NOT NULL, release text NOT NULL,
                correlation_id text, received_at timestamptz NOT NULL DEFAULT now());
            CREATE ROLE clockin_log_writer LOGIN PASSWORD 'test-logging-password';
            GRANT CONNECT ON DATABASE postgres TO clockin_log_writer;
            GRANT USAGE ON SCHEMA public TO clockin_log_writer;
            GRANT INSERT ON clockin_logs TO clockin_log_writer;
            """, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Restricted_writer_stores_structured_logs_without_credentials_or_activation_links()
    {
        var captured = new CaptureSink();
        var configuration = new LoggerConfiguration().WriteTo.Sink(captured);
        var connectionString = new NpgsqlConnectionStringBuilder(_database.GetConnectionString())
        {
            Username = "clockin_log_writer",
            Password = "test-logging-password",
        };
        PostgresLoggingModule.Configure(configuration, new PostgresLoggingOptions
        {
            Enabled = true,
            ConnectionString = connectionString.ConnectionString,
            Release = "test-release",
        }, "Testing");
        using (Logger logger = configuration.CreateLogger())
        {
            logger.ForContext("CorrelationId", "logging-check").ForContext("Elapsed", 12.5)
                .ForContext("Payload", new { Password = "nested-password" }, destructureObjects: true)
                .Warning(new InvalidOperationException("person@example.test password=exception-password Bearer exception-token"),
                    "Failed for {Email}; password=message-password Bearer message-token", "person@example.test");
            logger.Warning("First admin activation token={Token}", "private-activation-token");
        }

        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT message, exception, properties::text, service, environment, release, correlation_id FROM clockin_logs", connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        string stored = reader.GetString(0) + reader.GetString(1) + reader.GetString(2);
        stored.Should().Contain("[REDACTED]").And.NotContain("person@example.test")
            .And.NotContain("message-password").And.NotContain("exception-password")
            .And.NotContain("message-token").And.NotContain("exception-token").And.NotContain("nested-password");
        reader.GetString(2).Should().Contain("12.5").And.NotContain("Payload");
        reader.GetString(3).Should().Be("clockin");
        reader.GetString(4).Should().Be("Testing");
        reader.GetString(5).Should().Be("test-release");
        reader.GetString(6).Should().Be("logging-check");
        (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        captured.Events.Should().HaveCount(2);
        captured.Events[1].RenderMessage(CultureInfo.InvariantCulture).Should().Contain("private-activation-token");
    }

    [Fact]
    public void Unreachable_logging_database_does_not_block_log_calls()
    {
        var configuration = new LoggerConfiguration();
        PostgresLoggingModule.Configure(configuration, new PostgresLoggingOptions
        {
            Enabled = true,
            ConnectionString = "Host=127.0.0.1;Port=1;Database=unavailable;Username=writer;Password=test;Timeout=1;Pooling=false",
        }, "Testing");
        using Logger logger = configuration.CreateLogger();
        var elapsed = Stopwatch.StartNew();

        for (int index = 0; index < 1100; index++)
        {
            logger.Information("Outage check {Index}", index);
        }

        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
