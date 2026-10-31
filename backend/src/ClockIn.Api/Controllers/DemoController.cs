using ClockIn.Api.Demo;
using ClockIn.Application.Demo.Queries;
using ClockIn.Application.Demo.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wolverine;

namespace ClockIn.Api.Controllers;

/// <summary>Anonymous. The sign-in page lists these accounts so demo visitors can pick a role (ADR 0005).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/demo")]
public sealed class DemoController(IMessageBus bus, IOptions<DemoOptions> options) : ControllerBase
{
    /// <response code="404">Demo data is off: there are no demo accounts.</response>
    [HttpGet("accounts")]
    public async Task<ActionResult<DemoAccountsDto>> GetAccountsAsync(CancellationToken ct) =>
        options.Value.Enabled ? await bus.InvokeAsync<DemoAccountsDto>(new GetDemoAccountsQuery(), ct) : NotFound();
}
