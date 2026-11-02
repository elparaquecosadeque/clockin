using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.SecondPublicApi.Tests;

public sealed class FirstPublicApiFactory(SecondApiFactory secondApi) : WebApplicationFactory<PublicApi.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:Provider", "SQLite");
        builder.UseSetting("Database:ConnectionString", secondApi.ConnectionString);
        builder.UseSetting("Logging:LogLevel:Default", "Error");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(secondApi.Clock);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
                    notBefore <= secondApi.Clock.GetUtcNow().UtcDateTime && expires > secondApi.Clock.GetUtcNow().UtcDateTime);
        });
    }
}
