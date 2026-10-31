namespace ClockIn.Application.Reporting.Results;

/// <summary>A team, or the supervisors (<see cref="IsSupervisors"/>, admins only) as their own group.</summary>
public sealed record TeamWeekGroupDto(
    Guid? TeamId, string? TeamName, string? SupervisorName, bool IsSupervisors, WeekSummaryDto Summary, IReadOnlyList<TeamWeekRowDto> Rows);
