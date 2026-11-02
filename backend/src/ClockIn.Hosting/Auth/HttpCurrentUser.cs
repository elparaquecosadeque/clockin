using System.Security.Claims;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClockIn.Hosting.Auth;

/// <summary>The signed-in user of the current HTTP request. Only reached behind authorization.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public UserId Id => new(Guid.Parse(
        accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("No signed-in user on this request.")));
}
