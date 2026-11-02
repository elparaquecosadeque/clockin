using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NpgsqlTypes;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;
using Serilog.Sinks.PostgreSQL.ColumnWriters;

namespace ClockIn.Api.Logging;

public static partial class PostgresLoggingModule
{
    private static readonly string[] SafeProperties =
        ["SourceContext", "CorrelationId", "RequestMethod", "RequestPath", "StatusCode", "Elapsed", "UserId", "ActorId", "Role"];

    private static readonly string[] SensitiveProperties =
        ["Token", "AccessToken", "RefreshToken", "Password", "Authorization", "Cookie", "SigningKey", "ConnectionString"];

    public static void Configure(LoggerConfiguration configuration, PostgresLoggingOptions options, string environment)
    {
        if (!options.Enabled)
        {
            return;
        }

        Serilog.Debugging.SelfLog.Enable(_ => Console.Error.WriteLine("PostgreSQL log delivery failed; console logging remains available."));
        configuration.WriteTo.Logger(database => database
            .Filter.ByExcluding(log => log.Properties.Keys.Any(key => SensitiveProperties.Contains(key, StringComparer.OrdinalIgnoreCase)))
            .Enrich.WithProperty("Service", "clockin")
            .Enrich.WithProperty("Environment", environment)
            .Enrich.WithProperty("Release", options.Release)
            .WriteTo.PostgreSQL(options.ConnectionString, "clockin_logs", new Dictionary<string, ColumnWriterBase>
            {
                ["timestamp"] = new TimestampColumnWriter(NpgsqlDbType.TimestampTz),
                ["level"] = new LevelColumnWriter(true, NpgsqlDbType.Text),
                ["message"] = new SafeTextColumnWriter(log => log.RenderMessage(CultureInfo.InvariantCulture)),
                ["message_template"] = new SafeTextColumnWriter(log => log.MessageTemplate.Text),
                ["exception"] = new SafeTextColumnWriter(log => log.Exception?.ToString()),
                ["properties"] = new SafePropertiesColumnWriter(),
                ["service"] = new SinglePropertyColumnWriter("Service", PropertyWriteMethod.Raw, NpgsqlDbType.Text),
                ["environment"] = new SinglePropertyColumnWriter("Environment", PropertyWriteMethod.Raw, NpgsqlDbType.Text),
                ["release"] = new SinglePropertyColumnWriter("Release", PropertyWriteMethod.Raw, NpgsqlDbType.Text),
                ["correlation_id"] = new SafeTextColumnWriter(log =>
                    log.Properties.GetValueOrDefault("CorrelationId") is ScalarValue value ? value.Value?.ToString() : null),
            }, needAutoCreateTable: false, needAutoCreateSchema: false, queueLimit: 1000, batchSizeLimit: 50,
                period: TimeSpan.FromSeconds(2), useCopy: true, formatProvider: CultureInfo.InvariantCulture));
    }

    private static string Redact(string text) => SensitiveValues().Replace(
        AuthHeaders().Replace(Emails().Replace(text, "[REDACTED]"), "$1 [REDACTED]"), "$1=[REDACTED]");

    [GeneratedRegex(@"\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b", RegexOptions.None, 100)]
    private static partial Regex Emails();

    [GeneratedRegex(@"\b(Bearer|Basic)\s+\S+", RegexOptions.IgnoreCase, 100)]
    private static partial Regex AuthHeaders();

    [GeneratedRegex("\\b(password|pwd|token|secret|authorization|signingkey)\\s*[:=]\\s*(\"[^\"]*\"|'[^']*'|[^;\\s,]+)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex SensitiveValues();

    private sealed class SafeTextColumnWriter(Func<LogEvent, string?> read) : ColumnWriterBase(NpgsqlDbType.Text)
    {
        public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null) =>
            read(logEvent) is { } text ? Redact(text) : DBNull.Value;
    }

    private sealed class SafePropertiesColumnWriter() : ColumnWriterBase(NpgsqlDbType.Jsonb)
    {
        public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null) =>
            JsonSerializer.Serialize(logEvent.Properties
                .Where(property => SafeProperties.Contains(property.Key, StringComparer.Ordinal) && property.Value is ScalarValue)
                .ToDictionary(property => property.Key, property =>
                    ((ScalarValue)property.Value).Value is string text ? Redact(text) : ((ScalarValue)property.Value).Value));
    }
}
