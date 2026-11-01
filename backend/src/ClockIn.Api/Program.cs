using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ClockIn.Api.Auth;
using ClockIn.Api.Common;
using ClockIn.Api.Demo;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Common.Middleware;
using ClockIn.Application.Timesheets;
using ClockIn.Infrastructure.Common;
using ClockIn.Persistence.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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
        AddOptions(builder);
        AddModules(builder.Services, builder.Configuration);
        AddMediator(builder.Services);
        AddAuth(builder.Services);
        AddRateLimiting(builder.Services);
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

    private static void AddOptions(WebApplicationBuilder builder)
    {
        // Locally, a throwaway signing key: tokens die with the process, and no key is ever committed.
        if (builder.Environment.IsDevelopment() && string.IsNullOrEmpty(builder.Configuration["Auth:SigningKey"]))
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            builder.Configuration["Auth:SigningKey"] = key.ExportECPrivateKeyPem();
        }

        builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<CompanyOptions>().BindConfiguration(CompanyOptions.Section).ValidateDataAnnotations()
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Company:TimeZone is not a known timezone id.")
            .ValidateOnStart();

        // Demo data wipes the database (ADR 0005), so a real deployment refuses to start with it on.
        bool demoAllowed = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment(DemoOptions.Environment);
        builder.Services.AddOptions<DemoOptions>().BindConfiguration(DemoOptions.Section)
            .Validate(o => !o.Enabled || demoAllowed, "Demo:Enabled is only allowed in the Development and Demo environments.")
            .ValidateOnStart();
    }

    private static void AddModules(IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration).AddInfrastructure();
        services.AddScoped<TimesheetAccess>();
        AddDomainEventSubscribers(services);
    }

    /// <summary>Plain registrations, so the mediator builds them in the command's scope and unit of work.</summary>
    private static void AddDomainEventSubscribers(IServiceCollection services)
    {
        foreach (Type type in typeof(IDomainEventSubscriber).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IDomainEventSubscriber))))
        {
            services.AddScoped(typeof(IDomainEventSubscriber), type);
        }
    }

    private static void AddMediator(IServiceCollection services) =>
        services.AddWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(NotFoundException).Assembly);
            // Subscribers run inside the unit of work, not as mediator messages.
            opts.Discovery.CustomizeHandlerDiscovery(query => query.Excludes.Implements<IDomainEventSubscriber>());
            opts.Durability.Mode = DurabilityMode.MediatorOnly;
            opts.UseFluentValidation();
            opts.Policies.AddMiddleware(typeof(TransactionMiddleware), chain => chain.MessageType.IsAssignableTo(typeof(ITransactional)));
        })
        .AddHostedService<DemoDataService>() // before the first-admin seed, which then finds the demo admin
        .AddHostedService<FirstAdminSeeder>(); // after Wolverine's own hosted service, so the mediator is running

    private static void AddAuth(IServiceCollection services)
    {
        services.AddSingleton<AccessTokenIssuer>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<AccessTokenIssuer>((options, issuer) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = AccessTokenIssuer.Issuer,
                    ValidAudience = AccessTokenIssuer.Audience,
                    IssuerSigningKey = issuer.Key,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = CurrentUserCheckHelper.OnTokenValidatedAsync };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.Configure<AuthorizationOptions>(options =>
        {
            foreach (string policy in PoliciesConstants.All)
            {
                options.AddPolicy(policy, p => p.RequireClaim(PoliciesConstants.PermissionClaim, policy));
            }

            options.AddPolicy(PoliciesConstants.Review, p => p.RequireClaim(PoliciesConstants.PermissionClaim, PoliciesConstants.ReviewTeam, PoliciesConstants.ReviewAll));
        });
    }

    private static void AddRateLimiting(IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitsConstants.SignIn, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.SignInPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });

    private static void AddApi(IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["correlationId"] = CorrelationIdMiddleware.Get(ctx.HttpContext));
        services.AddExceptionHandler<ExceptionHandler>();
        services.AddSingleton<TimeProvider>(sp => new CompanyTimeProvider(
            TimeZoneInfo.FindSystemTimeZoneById(sp.GetRequiredService<IOptions<CompanyOptions>>().Value.TimeZone)));
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
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
    }

    private static void MapEndpoints(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
        app.MapControllers();
    }
}
