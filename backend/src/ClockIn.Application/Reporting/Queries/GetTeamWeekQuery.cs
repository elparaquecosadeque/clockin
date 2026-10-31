using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Results;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Accounts;
using FluentValidation;

namespace ClockIn.Application.Reporting.Queries;

/// <summary>One row per person and day, Drafts included so the grid is live. Admins may filter by team.</summary>
public sealed record GetTeamWeekQuery(DateOnly WeekStart, Guid? TeamId);

public sealed class GetTeamWeekValidator : AbstractValidator<GetTeamWeekQuery>
{
    public GetTeamWeekValidator() =>
        RuleFor(q => q.WeekStart).Must(d => d.DayOfWeek == DayOfWeek.Monday).WithMessage("A week starts on a Monday.");
}

public sealed class GetTeamWeekHandler(ICurrentUser currentUser, IReportingReads reads, TimeProvider time)
{
    public async Task<TeamWeekDto> HandleAsync(GetTeamWeekQuery query, CancellationToken ct)
    {
        ReportingScope scope = await ReportingScope.LoadAsync(currentUser, reads, ct);
        TeamRow? filter = scope.FilterTeam(query.TeamId);

        List<(TeamRow? Team, List<PersonRow> Members)> groups = [];
        IEnumerable<TeamRow> teams = filter is not null ? [filter] : scope.IsAdmin ? scope.ActiveTeams : [];
        foreach (TeamRow team in teams)
        {
            groups.Add((team, [.. scope.People.Where(p => p.Role == Role.Employee && p.TeamId == team.Id)]));
        }

        if (scope.IsAdmin && filter is null)
        {
            groups.Add((null, [.. scope.People.Where(p => p.Role == Role.Supervisor)]));
        }

        var timesheets = (await reads.TimesheetsAsync(
            [.. groups.SelectMany(g => g.Members).Select(p => p.Id)], query.WeekStart, ct)).ToDictionary(t => t.OwnerId);
        DateTimeOffset now = time.GetUtcNow();

        return new TeamWeekDto(
            query.WeekStart,
            [.. groups.Select(g => Group(g.Team, g.Members, scope, timesheets, query.WeekStart, now))],
            time.LocalTimeZone.Id);
    }

    private TeamWeekGroupDto Group(
        TeamRow? team, List<PersonRow> members, ReportingScope scope, Dictionary<Guid, TimesheetRow> timesheets, DateOnly weekStart,
        DateTimeOffset now)
    {
        // Deactivated people appear only in weeks they have a timesheet for.
        List<TeamWeekRowDto> rows = [.. members
            .Where(p => p.Status != UserStatus.Deactivated || timesheets.ContainsKey(p.Id))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => Row(p, timesheets.GetValueOrDefault(p.Id), weekStart, now))];
        return new TeamWeekGroupDto(
            team?.Id,
            team?.Name,
            team is null ? null : scope.People.FirstOrDefault(p => p.Id == team.SupervisorId)?.Name,
            team is null,
            new WeekSummaryDto(
                rows.Count(r => r.Status == WeekStatus.NotStarted),
                rows.Count(r => r.Status == WeekStatus.Draft),
                rows.Count(r => r.Status == WeekStatus.Submitted),
                rows.Count(r => r.Status == WeekStatus.Approved)),
            rows);
    }

    private TeamWeekRowDto Row(PersonRow person, TimesheetRow? timesheet, DateOnly weekStart, DateTimeOffset now)
    {
        IReadOnlyList<EntryRow> entries = timesheet?.Entries ?? [];
        long[] days = WorkedTime.PerDay(entries, weekStart, now, time.LocalTimeZone);
        EntryRow? open = entries.FirstOrDefault(e => e.End is null);
        return new TeamWeekRowDto(
            person.Id,
            person.Name,
            timesheet is null ? WeekStatus.NotStarted : Enum.Parse<WeekStatus>(timesheet.Status.ToString()),
            timesheet?.Id,
            days,
            open is null ? null : WorkedTime.Day(open.Start, time.LocalTimeZone).DayNumber - weekStart.DayNumber,
            days.Sum());
    }
}
