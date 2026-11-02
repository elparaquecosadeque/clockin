using System.Security.Claims;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClockIn.Hosting.Auth;

/// <summary>
/// Runs after the token signature checks out: rejects users who are no longer active or whose security stamp
/// changed, and tokens whose session was logged out or has expired (ADR 0009). Then adds their current permissions as claims (CODE_STYLE.md, Permissions are checked on every request).
/// </summary>
public static class CurrentUserCheckHelper
{
    // ponytail: a user and a session lookup per request; add a short cache invalidated on changes if it shows up in profiles.
    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        ClaimsPrincipal principal = context.Principal!;
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid userId)
            || !Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sid), out Guid sessionId)
            || !Guid.TryParse(principal.FindFirstValue(AccessTokenIssuer.SecurityStampClaim), out Guid stamp))
        {
            context.Fail("Malformed access token.");
            return;
        }

        IUserRepository usersRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        User? user = await usersRepository.FindAsync(new UserId(userId), context.HttpContext.RequestAborted);
        Session? session = await context.HttpContext.RequestServices.GetRequiredService<ISessionRepository>()
            .FindAsync(new SessionId(sessionId), context.HttpContext.RequestAborted);
        DateTimeOffset now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        if (user is not { Status: UserStatus.Active } || user.SecurityStamp != stamp
            || session is null || session.UserId != user.Id || !session.IsActive(now))
        {
            context.Fail("The session is no longer valid.");
            return;
        }

        principal.AddIdentity(new ClaimsIdentity(Permissions.For(user.Role).Select(p => new Claim(PoliciesConstants.PermissionClaim, p))));
    }
}
