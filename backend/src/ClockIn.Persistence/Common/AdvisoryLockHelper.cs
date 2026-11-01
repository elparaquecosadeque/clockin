using LinqToDB;
using LinqToDB.Data;

namespace ClockIn.Persistence.Common;

/// <summary>Locks held until the current transaction ends, on any supported engine.</summary>
public static class AdvisoryLockHelper
{
    /// <summary>Makes other holders of <paramref name="key"/> wait; two keys that hash alike only wait for each other.</summary>
    // ponytail: no lock on SQLite, which only runs in Development with one user; writers can still race there.
    public static Task LockAsync(DataConnection db, Guid key, CancellationToken ct) =>
        db.DataProvider.Name.StartsWith(ProviderName.PostgreSQL, StringComparison.Ordinal)
            ? db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@key::text, 0))", ct, new DataParameter("key", key))
            : Task.CompletedTask;
}
