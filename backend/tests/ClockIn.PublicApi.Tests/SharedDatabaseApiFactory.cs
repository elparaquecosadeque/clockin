using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.PublicApi.Tests;

/// <summary>The unchanged SPA host connected to the public host's prepared test database.</summary>
public sealed class SharedDatabaseApiFactory(PublicApiFactory publicApi) : WebApplicationFactory<Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:Provider", "SQLite");
        builder.UseSetting("Database:ConnectionString", publicApi.ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Demo:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(publicApi.Clock);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
                    notBefore <= publicApi.Clock.GetUtcNow().UtcDateTime && expires > publicApi.Clock.GetUtcNow().UtcDateTime);
        });
    }
}
