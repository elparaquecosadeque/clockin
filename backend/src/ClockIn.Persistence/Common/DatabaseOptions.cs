namespace ClockIn.Persistence.Common;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary><c>PostgreSQL</c> or <c>SQLite</c>.</summary>
    public required string Provider { get; init; }

    public required string ConnectionString { get; init; }

    /// <summary>True only locally and in tests; remote environments migrate as a deployment step.</summary>
    public bool MigrateOnStartup { get; init; }
}
