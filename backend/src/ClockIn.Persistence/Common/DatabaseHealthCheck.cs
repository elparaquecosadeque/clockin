using LinqToDB.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClockIn.Persistence.Common;

public sealed class DatabaseHealthCheck(AppDataConnection db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.ExecuteAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable.", ex);
        }
    }
}
