using ClockIn.Api.Tests.Common;
using ClockIn.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.Api.Tests.Demo;

/// <summary>The API in the Demo environment, over its own database, since the demo seed wipes every table.</summary>
public sealed class DemoApiFactory : ApiFactory
{
    /// <summary>A Friday at 10:00 in company time.</summary>
    public CompanyClock Clock { get; } = new("2026-10-30 10:00");

    protected override void ConfigureHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Demo");
        builder.UseSetting("Demo:Enabled", "true");
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }
}
