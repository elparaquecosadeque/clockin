using ClockIn.Application.Reporting.Queries;
using ClockIn.Application.Reporting.Results;
using ClockIn.Hosting.Auth;
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
    public Task<ReportPageDto<ExceptionDto>> GetExceptionsAsync([FromQuery] int page = 1, CancellationToken ct = default) =>
        bus.InvokeAsync<ReportPageDto<ExceptionDto>>(new GetExceptionsQuery(page), ct);

    [HttpGet("review-queue")]
    public Task<ReviewQueueDto> GetReviewQueueAsync([FromQuery] int timesheetPage = 1, [FromQuery] int correctionPage = 1, CancellationToken ct = default) =>
        bus.InvokeAsync<ReviewQueueDto>(new GetReviewQueueQuery(timesheetPage, correctionPage), ct);

    [HttpGet("approved-csv/{weekStart}")]
    public async Task<FileContentResult> ExportApprovedAsync(DateOnly weekStart, [FromQuery] Guid? teamId, CancellationToken ct)
    {
        CsvFileDto file = await bus.InvokeAsync<CsvFileDto>(new ExportApprovedCsvQuery(weekStart, teamId), ct);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }
}
