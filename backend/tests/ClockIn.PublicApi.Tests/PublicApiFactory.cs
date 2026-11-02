using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Demo.Interfaces;
using ClockIn.Persistence.Common;
using ClockIn.Tests.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ClockIn.PublicApi.Tests;

public sealed class PublicApiFactory(string environment = "Development", string provider = "SQLite") : WebApplicationFactory<Program>
{
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"clockin-public-{Guid.CreateVersion7():N}.db");
    private readonly PostgreSqlContainer? _postgres = provider == "PostgreSQL" ? new PostgreSqlBuilder("postgres:16-alpine").Build() : null;

    public CompanyClock Clock { get; } = new();

    public string ConnectionString => _postgres?.GetConnectionString() ?? $"Data Source={_database};Mode=ReadWrite;Pooling=False";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Database:Provider", provider);
        builder.UseSetting("Database:ConnectionString", ConnectionString);
        builder.UseSetting("Auth:SignInPermitsPerMinute", "1000");
        builder.UseSetting("Logging:LogLevel:Default", "Error");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
                    notBefore <= Clock.GetUtcNow().UtcDateTime && expires > Clock.GetUtcNow().UtcDateTime);
        });
    }

    public async Task PrepareAsync(CancellationToken ct)
    {
        if (_postgres is not null)
        {
            await _postgres.StartAsync(ct);
        }
        else
        {
            await File.WriteAllBytesAsync(_database, [], ct);
        }

        Services.MigrateDatabase();
        using IServiceScope scope = Services.CreateScope();
        IDemoData demo = scope.ServiceProvider.GetRequiredService<IDemoData>();
        await demo.ResetAsync(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(demo.Password), ct);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        File.Delete(_database);
    }
}
