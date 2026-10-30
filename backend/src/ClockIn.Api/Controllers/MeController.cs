using ClockIn.Application.Accounts.Queries;
using ClockIn.Application.Accounts.Results;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace ClockIn.Api.Controllers;

/// <summary>Any signed-in user; the frontend loads its session's permissions from here.</summary>
[ApiController]
[Route("api/me")]
public sealed class MeController(IMessageBus bus) : ControllerBase
{
    [HttpGet]
    public Task<MeDto> Get(CancellationToken ct) => bus.InvokeAsync<MeDto>(new GetMeQuery(), ct);
}
