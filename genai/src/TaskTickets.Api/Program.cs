using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Scalar.AspNetCore;
using TaskTickets.Api.Common;
using TaskTickets.Api.Data;
using TaskTickets.Api.Features.Auth;
using TaskTickets.Api.Features.Tasks;

namespace TaskTickets.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        // Keep framework protection keys disposable too, without reading the user's disk key ring.
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<InMemoryStore>();
        builder.Services.AddApiAuthentication(builder.Configuration);
        builder.Services.AddScoped<TaskService>();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<TicketStatus>(allowIntegerValues: false));
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        });
        builder.Services.AddValidation();
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddOpenApi(options => options.AddDocumentTransformer<ApiDocumentTransformer>());
        var app = builder.Build();
        DemoSeed.Initialize(app.Services);
        app.UseExceptionHandler();
        app.UseStatusCodePages(async context =>
            await Results.Problem(statusCode: context.HttpContext.Response.StatusCode).ExecuteAsync(context.HttpContext));
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAuthEndpoints();
        app.MapTaskEndpoints();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference(options => options.WithTitle("TaskTickets API").AddPreferredSecuritySchemes("Bearer")).AllowAnonymous();
        }
        app.Run();
    }
}
