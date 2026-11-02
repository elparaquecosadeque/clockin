using ClockIn.Application.Accounts.Commands;
using ClockIn.Hosting.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClockIn.SecondPublicApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    public sealed record LogInRequest(string Email, string Password);

    public sealed record SetPasswordRequest(string Token, string Password);

    /// <summary>Sign in with an active account; returns a 15-minute access token without refresh cookies.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitsConstants.SignIn)]
    public async Task<AccessTokenResponse> LogInAsync(
        LogInRequest request,
        [FromServices] LogInHandler handler,
        [FromServices] LogInValidator validator,
        [FromServices] AccessTokenIssuer tokens,
        CancellationToken ct)
    {
        var command = new LogInCommand(request.Email, request.Password);
        await validator.ValidateAndThrowAsync(command, ct);
        return tokens.Issue(await handler.HandleAsync(command, ct));
    }

    /// <summary>Set the initial password using the secret activation token returned to the creating admin.</summary>
    [HttpPost("set-password")]
    [EnableRateLimiting(RateLimitsConstants.SignIn)]
    public async Task<NoContentResult> SetPasswordAsync(
        SetPasswordRequest request,
        [FromServices] SetPasswordWithLinkHandler handler,
        [FromServices] SetPasswordWithLinkValidator validator,
        CancellationToken ct)
    {
        var command = new SetPasswordWithLinkCommand(request.Token, request.Password);
        await validator.ValidateAndThrowAsync(command, ct);
        await handler.HandleAsync(command, ct);
        return NoContent();
    }
}
