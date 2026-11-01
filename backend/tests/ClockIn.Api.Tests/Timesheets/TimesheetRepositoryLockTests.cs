using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Persistence.Common;
using ClockIn.Persistence.Timesheets;
using LinqToDB;

namespace ClockIn.Api.Tests.Timesheets;

/// <summary>The per-owner lock on the Development engine; the API tests cover it on PostgreSQL.</summary>
public sealed class TimesheetRepositoryLockTests
{
    [Fact]
    public async Task Locking_an_owner_is_a_no_op_on_sqlite()
    {
        await using var db = new AppDataConnection(new DataOptions().UseConnectionString(ProviderName.SQLiteMS, "Data Source=:memory:"));

        var act = () => new TimesheetRepository(db).LockOwnerAsync(UserId.New(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }
}
