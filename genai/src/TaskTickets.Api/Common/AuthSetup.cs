using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskTickets.Api.Data;
using TaskTickets.Api.Features.Auth;

namespace TaskTickets.Api.Common;

public static class AuthSetup
{
    public static void AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>().Bind(configuration.GetSection("Auth")).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AuthRateLimitOptions>().Bind(configuration.GetSection("AuthRateLimit")).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(_ => new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)));
        services.AddSingleton<PasswordHasher<User>>();
        services.AddSingleton<DummyPassword>();
        services.AddScoped<TokenIssuer>();
        services.AddScoped<AuthService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<SymmetricSecurityKey, IOptions<AuthOptions>, TimeProvider>((options, key, settings, clock) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidIssuer = settings.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Value.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                    {
                        var now = clock.GetUtcNow().UtcDateTime;
                        return expires.HasValue && (!notBefore.HasValue || notBefore <= now + parameters.ClockSkew)
                            && expires >= now - parameters.ClockSkew && (!notBefore.HasValue || notBefore <= expires);
                    }
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        var store = context.HttpContext.RequestServices.GetRequiredService<InMemoryStore>();
                        if (!Guid.TryParse(subject, out var id) || store.FindUser(id) is null)
                        {
                            context.Fail("Invalid user.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("auth", context =>
            {
                var limits = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PerIp,
                    Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                    QueueLimit = 0
                });
            });
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName != "auth")
                {
                    return RateLimitPartition.GetNoLimiter("other");
                }

                var limits = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter("auth", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.Global,
                    Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                    QueueLimit = 0
                });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay) ? Math.Ceiling(delay.TotalSeconds) : 60;
                context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, retry).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: 429, title: "Too many authentication requests.").ExecuteAsync(context.HttpContext);
            };
        });
    }
}
