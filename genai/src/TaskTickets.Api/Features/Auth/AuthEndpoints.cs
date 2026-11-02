using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace TaskTickets.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");
        group.MapPost("/register", (RegisterRequest request, AuthService service) =>
            TypedResults.Created("/api/auth/me", service.Register(request)))
            .AllowAnonymous().RequireRateLimiting("auth").WithName("Register").WithSummary("Register a user; then log in to obtain a token.")
            .ProducesValidationProblem().ProducesProblem(409).ProducesProblem(429);
        group.MapPost("/login", (LoginRequest request, AuthService service) => TypedResults.Ok(service.Login(request)))
            .AllowAnonymous().RequireRateLimiting("auth").WithName("Login").WithSummary("Log in with email and password.")
            .ProducesValidationProblem().ProducesProblem(401).ProducesProblem(429);
        group.MapGet("/me", (ClaimsPrincipal caller, AuthService service) => TypedResults.Ok(service.Me(Guid.Parse(caller.FindFirstValue("sub")!))))
            .WithName("Me").WithSummary("Read the current user's public profile.").ProducesProblem(401);
    }
}
