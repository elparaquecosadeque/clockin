using System.Security.Claims;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClockIn.Api.Auth;

/// <summary>
/// Runs after the token signature checks out: rejects users who are no longer active or whose security stamp
/// changed, and adds their current permissions as claims (CODE_STYLE.md, Permissions are checked on every request).
/// </summary>
public static class CurrentUserCheck
{
    // ponytail: one user lookup per request; add a short cache invalidated on permission changes if it shows up in profiles.
    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        ClaimsPrincipal principal = context.Principal!;
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid userId)
            || !Guid.TryParse(principal.FindFirstValue(AccessTokenIssuer.SecurityStampClaim), out Guid stamp))
        {
            context.Fail("Malformed access token.");
            return;
        }

        IUserRepository users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        User? user = await users.FindAsync(new UserId(userId), context.HttpContext.RequestAborted);
        if (user is not { Status: UserStatus.Active } || user.SecurityStamp != stamp)
        {
            context.Fail("The session is no longer valid.");
            return;
        }

        principal.AddIdentity(new ClaimsIdentity(Permissions.For(user.Role).Select(p => new Claim(Policies.PermissionClaim, p))));
    }
}
