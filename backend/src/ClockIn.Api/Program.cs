using System.Text.Json.Serialization;
using ClockIn.Api.Common;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Persistence.Common;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Wolverine;
using Wolverine.FluentValidation;

namespace ClockIn.Api;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        AddLogging(builder);
        AddModules(builder.Services, builder.Configuration);
        AddMediator(builder.Services);
        AddApi(builder.Services);
        AddApiDocs(builder.Services);

        var app = builder.Build();
        MigrateIfConfigured(app);
        UsePipeline(app);
        MapEndpoints(app);
        app.Run();
    }

    private static void AddLogging(WebApplicationBuilder builder) =>
        builder.Services.AddSerilog((services, config) => config
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services));

    private static void AddModules(IServiceCollection services, IConfiguration configuration) =>
        services.AddPersistence(configuration);

    private static void AddMediator(IServiceCollection services) =>
        services.AddWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(NotFoundException).Assembly);
            opts.Durability.Mode = DurabilityMode.MediatorOnly;
            opts.UseFluentValidation();
        });

    private static void AddApi(IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["correlationId"] = CorrelationIdMiddleware.Get(ctx.HttpContext));
        services.AddExceptionHandler<ExceptionHandler>();
        services.AddSingleton(TimeProvider.System);
    }

    private static void AddApiDocs(IServiceCollection services) => services.AddOpenApi();

    private static void MigrateIfConfigured(WebApplication app)
    {
        if (app.Services.GetRequiredService<DatabaseOptions>().MigrateOnStartup)
        {
            app.Services.MigrateDatabase();
        }
    }

    private static void UsePipeline(WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
    }

    private static void MapEndpoints(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        app.MapControllers();
    }
}
