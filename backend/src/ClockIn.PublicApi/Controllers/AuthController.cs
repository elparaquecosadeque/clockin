using ClockIn.Application.Accounts.Commands;
using ClockIn.Hosting.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClockIn.PublicApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    LogInHandler handler,
    LogInValidator validator,
    AccessTokenIssuer tokens) : ControllerBase
{
    public sealed record LogInRequest(string Email, string Password);

    /// <summary>Sign in with an existing demo account. No refresh token or cookie is returned.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitsConstants.SignIn)]
    public async Task<AccessTokenResponse> LogInAsync(LogInRequest request, CancellationToken ct)
    {
        var command = new LogInCommand(request.Email, request.Password);
        await validator.ValidateAndThrowAsync(command, ct);
        return tokens.Issue(await handler.HandleAsync(command, ct));
    }
}
