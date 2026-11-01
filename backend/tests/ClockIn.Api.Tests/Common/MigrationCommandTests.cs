using AwesomeAssertions;
using ClockIn.Persistence.Accounts;
using ClockIn.Persistence.Common;
using ClockIn.Persistence.Timesheets;
using LinqToDB;
using LinqToDB.Async;

namespace ClockIn.Api.Tests.Common;

public sealed class MigrationCommandTests
{
    [Fact]
    public async Task Migration_command_creates_schema_without_seeding_and_can_be_repeated()
    {
        string databaseFile = Path.GetTempFileName();
        string connectionString = $"Data Source={databaseFile};Pooling=False";
        try
        {
            string[] arguments = ["migrate", "--Database:Provider=SQLite", $"--Database:ConnectionString={connectionString}"];
            Program.Main(arguments);
            Program.Main(arguments);

            await using var db = new AppDataConnection(new DataOptions().UseConnectionString(ProviderName.SQLiteMS, connectionString));
            (await db.GetTable<UserRecord>().CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
            (await db.GetTable<TimesheetRecord>().CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }
        finally
        {
            File.Delete(databaseFile);
        }
    }

    [Fact]
    public void Migration_command_propagates_configuration_failures()
    {
        Action migrate = () => Program.Main(["migrate", "--Database:Provider=unsupported", "--Database:ConnectionString=unused"]);

        migrate.Should().Throw<InvalidOperationException>();
    }
}
