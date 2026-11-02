using ClockIn.Application.Accounts.Queries;
using ClockIn.Application.Accounts.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClockIn.SecondPublicApi.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class MeController(GetMeHandler handler) : ControllerBase
{
    /// <summary>Return the authenticated user's identity, current permissions and team details.</summary>
    [HttpGet]
    public Task<MeDto> GetAsync(CancellationToken ct) => handler.HandleAsync(new GetMeQuery(), ct);
}
