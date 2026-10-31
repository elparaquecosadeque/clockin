using ClockIn.Persistence.Accounts;
using ClockIn.Persistence.Teams;
using ClockIn.Persistence.Timesheets;
using FluentMigrator.Runner;
using LinqToDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClockIn.Persistence.Common;

public static class PersistenceModule
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        DatabaseOptions options = configuration.GetRequiredSection(DatabaseOptions.Section).Get<DatabaseOptions>()
            ?? throw new InvalidOperationException($"Missing '{DatabaseOptions.Section}' settings.");
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException($"'{DatabaseOptions.Section}:ConnectionString' is empty. Set it in user secrets or the environment.");
        }

        services.AddSingleton(options);

        // The only place that knows about engines (CODE_STYLE.md, Database engines).
        bool isPostgres = options.Provider switch
        {
            "PostgreSQL" => true,
            "SQLite" => false,
            _ => throw new InvalidOperationException($"Unsupported database provider '{options.Provider}'. Use PostgreSQL or SQLite."),
        };
        string linqProvider = isPostgres ? ProviderName.PostgreSQL : ProviderName.SQLiteMS;

        // Plain constructor registrations: Wolverine generates handler code and cannot call factory lambdas.
        services.AddSingleton(new DataOptions().UseConnectionString(linqProvider, options.ConnectionString));
        services.AddScoped<AppDataConnection>();

        services.AddFluentMigratorCore().ConfigureRunner(rb => (isPostgres ? rb.AddPostgres() : rb.AddSQLite())
            .WithGlobalConnectionString(options.ConnectionString)
            .ScanIn(typeof(PersistenceModule).Assembly).For.Migrations());

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, ["ready"]);
        return services.AddTimesheetsPersistence().AddAccountsPersistence().AddTeamsPersistence();
    }

    public static void MigrateDatabase(this IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    }
}
