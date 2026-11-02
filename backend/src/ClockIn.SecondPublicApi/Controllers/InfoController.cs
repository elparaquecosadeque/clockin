using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClockIn.SecondPublicApi.Controllers;

[ApiController]
[Route("api/v1/info")]
[AllowAnonymous]
public sealed class InfoController : ControllerBase
{
    public sealed record InfoResponse(string Name, string Version);

    /// <summary>Public API name and version. No account or database information is exposed.</summary>
    [HttpGet]
    public InfoResponse Get() => new("ClockIn.SecondPublicApi", "v1");
}
