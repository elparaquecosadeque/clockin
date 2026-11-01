using Microsoft.Data.Sqlite;
using Npgsql;

namespace ClockIn.Persistence.Common;

/// <summary>Recognizes a unique-index violation on any supported engine.</summary>
public static class UniqueViolationHelper
{
    private const int SqliteConstraintUnique = 2067;

    public static bool Is(Exception ex) => ex
        is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        or SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique };
}
