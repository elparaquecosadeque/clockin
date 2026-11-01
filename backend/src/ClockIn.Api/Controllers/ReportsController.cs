using ClockIn.Api.Auth;
using ClockIn.Application.Reporting.Queries;
using ClockIn.Application.Reporting.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace ClockIn.Api.Controllers;

/// <summary>Supervisors and admins. The handlers scope each report to the team led, or everyone for admins.</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = PoliciesConstants.Review)]
public sealed class ReportsController(IMessageBus bus) : ControllerBase
{
    [HttpGet("team-week/{weekStart}")]
    public Task<TeamWeekDto> GetTeamWeekAsync(DateOnly weekStart, [FromQuery] Guid? teamId, CancellationToken ct) =>
        bus.InvokeAsync<TeamWeekDto>(new GetTeamWeekQuery(weekStart, teamId), ct);

    [HttpGet("clocked-in")]
    public Task<IReadOnlyList<ClockedInDto>> GetClockedInAsync(CancellationToken ct) =>
        bus.InvokeAsync<IReadOnlyList<ClockedInDto>>(new GetClockedInQuery(), ct);

    [HttpGet("exceptions")]
    public Task<IReadOnlyList<ExceptionDto>> GetExceptionsAsync(CancellationToken ct) =>
        bus.InvokeAsync<IReadOnlyList<ExceptionDto>>(new GetExceptionsQuery(), ct);

    [HttpGet("review-queue")]
    public Task<ReviewQueueDto> GetReviewQueueAsync(CancellationToken ct) =>
        bus.InvokeAsync<ReviewQueueDto>(new GetReviewQueueQuery(), ct);

    [HttpGet("approved-csv/{weekStart}")]
    public async Task<FileContentResult> ExportApprovedAsync(DateOnly weekStart, [FromQuery] Guid? teamId, CancellationToken ct)
    {
        CsvFileDto file = await bus.InvokeAsync<CsvFileDto>(new ExportApprovedCsvQuery(weekStart, teamId), ct);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }
}
