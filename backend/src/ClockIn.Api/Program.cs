using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ClockIn.Api.Auth;
using ClockIn.Api.Common;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
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
    }

    private static void AddModules(IServiceCollection services, IConfiguration configuration) =>
        services.AddPersistence(configuration).AddInfrastructure();

    private static void AddMediator(IServiceCollection services) =>
        services.AddWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(NotFoundException).Assembly);
            opts.Durability.Mode = DurabilityMode.MediatorOnly;
            opts.UseFluentValidation();
        }).AddHostedService<FirstAdminSeeder>(); // after Wolverine's own hosted service, so the mediator is running

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
                options.Events = new JwtBearerEvents { OnTokenValidated = CurrentUserCheck.OnTokenValidatedAsync };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.Configure<AuthorizationOptions>(options =>
        {
            foreach (string policy in Policies.All)
            {
                options.AddPolicy(policy, p => p.RequireClaim(Policies.PermissionClaim, policy));
            }
        });
    }

    private static void AddRateLimiting(IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimits.SignIn, context => RateLimitPartition.GetFixedWindowLimiter(
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
