using FluentMigrator;
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

        services.AddScoped(_ => new AppDataConnection(new DataOptions().UseConnectionString(linqProvider, options.ConnectionString)));

        services.AddFluentMigratorCore().ConfigureRunner(rb => (isPostgres ? rb.AddPostgres() : rb.AddSQLite())
            .WithGlobalConnectionString(options.ConnectionString)
            .ScanIn(typeof(PersistenceModule).Assembly).For.Migrations());

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, ["ready"]);
        return services;
    }

    public static void MigrateDatabase(this IServiceProvider services)
    {
        // ponytail: FluentMigrator throws when it finds no migrations; drop this guard once the first one exists.
        if (!typeof(PersistenceModule).Assembly.GetTypes().Any(t => t.IsDefined(typeof(MigrationAttribute), inherit: false)))
        {
            return;
        }

        using IServiceScope scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    }
}
