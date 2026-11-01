using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ClockIn.Api.Tests.Common;

/// <summary>The real API over a disposable PostgreSQL container, one per test run.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string FirstAdminEmail = "admin@clockin.test";

    private static readonly string SigningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportECPrivateKeyPem();

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async ValueTask InitializeAsync() => await _database.StartAsync();

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "PostgreSQL");
        builder.UseSetting("Database:ConnectionString", _database.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningKey", SigningKey);
        builder.UseSetting("FirstAdmin:Email", FirstAdminEmail);
        // Every test signs in from the same client; only the rate-limit test lowers this.
        builder.UseSetting("Auth:SignInPermitsPerMinute", "10000");
        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly));
        ConfigureHost(builder);
    }

    /// <summary>For factories that need a different host, each over its own database.</summary>
    protected virtual void ConfigureHost(IWebHostBuilder builder)
    {
    }
}
