using ClockIn.Api.Auth;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Results;
using ClockIn.Hosting.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace ClockIn.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(IMessageBus bus, AccessTokenIssuer tokens) : ControllerBase
{
    public sealed record LogInRequest(string Email, string Password);

    public sealed record SetPasswordRequest(string Token, string Password);

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitsConstants.SignIn)]
    public async Task<AccessTokenResponse> LogInAsync(LogInRequest request, CancellationToken ct) =>
        SignedIn(await bus.InvokeAsync<SessionResult>(new LogInCommand(request.Email, request.Password), ct));

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitsConstants.SignIn)]
    public async Task<AccessTokenResponse> RefreshAsync(CancellationToken ct) =>
        SignedIn(await bus.InvokeAsync<SessionResult>(new RefreshSessionCommand(RefreshCookieHelper.Read(Request)), ct));

    [HttpPost("logout")]
    public async Task<NoContentResult> LogOutAsync(CancellationToken ct)
    {
        await bus.InvokeAsync(new LogOutCommand(RefreshCookieHelper.Read(Request)), ct);
        RefreshCookieHelper.Delete(Response);
        return NoContent();
    }

    [HttpPost("set-password")]
    [EnableRateLimiting(RateLimitsConstants.SignIn)]
    public async Task<NoContentResult> SetPasswordAsync(SetPasswordRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetPasswordWithLinkCommand(request.Token, request.Password), ct);
        return NoContent();
    }

    private AccessTokenResponse SignedIn(SessionResult session)
    {
        RefreshCookieHelper.Append(Response, session);
        return tokens.Issue(session);
    }
}
