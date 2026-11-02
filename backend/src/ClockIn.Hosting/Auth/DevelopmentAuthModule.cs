using System.Threading.RateLimiting;
using ClockIn.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClockIn.Hosting.Auth;

public static class DevelopmentAuthModule
{
    public static void AddDevelopmentUserAuth(this IServiceCollection services, string audience, string permission)
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
                    ValidAudience = audience,
                    IssuerSigningKey = issuer.Key,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = CurrentUserCheckHelper.OnTokenValidatedAsync };
            });
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(permission, p => p.RequireClaim(PoliciesConstants.PermissionClaim, permission));
    }

    public static void AddSignInRateLimiting(this IServiceCollection services) =>
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
}
