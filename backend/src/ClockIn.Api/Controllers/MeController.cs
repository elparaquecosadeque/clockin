using ClockIn.Api.Auth;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Queries;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Reporting.Queries;
using ClockIn.Application.Reporting.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace ClockIn.Api.Controllers;

/// <summary>Any signed-in user; the frontend loads its session's permissions from here.</summary>
[ApiController]
[Route("api/me")]
public sealed class MeController(IMessageBus bus, AccessTokenIssuer tokens) : ControllerBase
{
    [HttpGet]
    public Task<MeDto> Get(CancellationToken ct) => bus.InvokeAsync<MeDto>(new GetMeQuery(), ct);

    [HttpGet("to-dos")]
    public Task<ToDosDto> GetToDosAsync(CancellationToken ct) => bus.InvokeAsync<ToDosDto>(new GetToDosQuery(), ct);

    [HttpPut("preferences")]
    public async Task<NoContentResult> UpdatePreferencesAsync(UpdatePreferencesCommand command, CancellationToken ct)
    {
        await bus.InvokeAsync(command, ct);
        return NoContent();
    }

    /// <summary>Every other session ends; this one gets new tokens.</summary>
    [HttpPost("password")]
    [EnableRateLimiting(RateLimits.SignIn)]
    public async Task<AccessTokenResponse> ChangePasswordAsync(ChangeOwnPasswordCommand command, CancellationToken ct)
    {
        SessionResult session = await bus.InvokeAsync<SessionResult>(command, ct);
        RefreshCookie.Append(Response, session);
        return tokens.Issue(session);
    }
}
