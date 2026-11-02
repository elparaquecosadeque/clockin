using System.Security.Cryptography;
using System.Text.Json.Serialization;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Queries;
using ClockIn.Hosting.Auth;
using ClockIn.Hosting.Common;
using ClockIn.Infrastructure.Common;
using ClockIn.Persistence.Common;
using ClockIn.SecondPublicApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace ClockIn.SecondPublicApi;

public sealed class Program
{
    public const string Audience = "ClockIn.SecondPublicApi";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("ClockIn.SecondPublicApi may only run in Development.");
        }

        AddOptions(builder);
        AddModules(builder.Services, builder.Configuration);
        AddAuth(builder.Services);
        AddRateLimiting(builder.Services);
        AddApi(builder.Services);
        AddApiDocs(builder.Services);

        var app = builder.Build();
        UsePipeline(app);
        MapEndpoints(app);
        app.Run();
    }

    private static void AddOptions(WebApplicationBuilder builder)
    {
        // A separate process-local key deliberately prevents tokens being shared with the SPA host.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        builder.Configuration["Auth:SigningKey"] = key.ExportECPrivateKeyPem();
        builder.Configuration["Auth:Audience"] = Audience;
        builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<CompanyOptions>().BindConfiguration(CompanyOptions.Section).ValidateDataAnnotations()
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Company:TimeZone is not a known timezone id.")
            .ValidateOnStart();
        if (string.IsNullOrWhiteSpace(builder.Configuration["Database:ConnectionString"]))
        {
            // Match the SPA host's existing SQLite file, independent of the shell's current directory.
            string database = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../ClockIn.Api/clockin.dev.db"));
            builder.Configuration["Database:ConnectionString"] = $"Data Source={database};Mode=ReadWrite";
        }
    }

    private static void AddModules(IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration).AddInfrastructure();
        services.AddScoped<LogInHandler>();
        services.AddScoped<LogInValidator>();
        services.AddScoped<SetPasswordWithLinkHandler>();
        services.AddScoped<SetPasswordWithLinkValidator>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<CreateUserValidator>();
        services.AddScoped<GetMeHandler>();
    }

    private static void AddAuth(IServiceCollection services) =>
        services.AddDevelopmentUserAuth(Audience, PoliciesConstants.ManageUsers);

    private static void AddRateLimiting(IServiceCollection services) => services.AddSignInRateLimiting();

    private static void AddApi(IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["correlationId"] = CorrelationIdMiddleware.Get(ctx.HttpContext));
        services.AddExceptionHandler<ExceptionHandler>();
        services.AddSingleton<TimeProvider>(sp => new CompanyTimeProvider(
            TimeZoneInfo.FindSystemTimeZoneById(sp.GetRequiredService<IOptions<CompanyOptions>>().Value.TimeZone)));
    }

    private static void AddApiDocs(IServiceCollection services) => services.AddOpenApi("v1", options =>
    {
        options.AddDocumentTransformer<AccountExamplesTransformer>();
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "ClockIn second public API";
            document.Info.Version = "v1";
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Sign in at /api/v1/auth/login. Paste the accessToken; it expires after 15 minutes.",
            };
            return Task.CompletedTask;
        });
        options.AddOperationTransformer((operation, context, _) =>
        {
            if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            {
                operation.Security = [new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
                }];
            }

            return Task.CompletedTask;
        });
    });

    private static void UsePipeline(WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        // JSON's restrictive CSP would prevent Scalar's interactive UI from loading.
        app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch => branch.UseMiddleware<SecurityHeadersMiddleware>());
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options => options.WithTitle("ClockIn SecondPublicApi · Development").AddPreferredSecuritySchemes("Bearer"))
            .AllowAnonymous();
        app.MapControllers();
    }
}
