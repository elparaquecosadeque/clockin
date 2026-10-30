using LinqToDB;
using LinqToDB.Data;

namespace ClockIn.Persistence.Common;

public sealed class AppDataConnection(DataOptions options) : DataConnection(options);
