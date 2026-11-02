namespace ClockIn.Api.Logging;

public sealed class PostgresLoggingOptions
{
    public const string Section = "PostgresLogging";

    public bool Enabled { get; init; }

    public string ConnectionString { get; init; } = "";

    public string Release { get; init; } = "local";
}
