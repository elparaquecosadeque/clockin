using AwesomeAssertions;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Queries;
using ClockIn.Application.Reporting.Results;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Tests.Reporting;

/// <summary>team-week-grid.feature.</summary>
public sealed class TeamWeekHandlerTests
{
    private readonly ReportingWorld _world = new();

    [Fact]
    public async Task Shows_the_supervisors_team_with_day_totals_and_not_started_rows()
    {
        _world.Timesheet(_world.Eve, ReportingWorld.ThisWeek, entries:
            [("2026-10-26 08:00", "2026-10-26 16:00"), ("2026-10-27 08:00", "2026-10-27 15:30")]);

        TeamWeekDto grid = await Grid(ReportingWorld.ThisWeek);

        TeamWeekGroupDto group = grid.Groups.Should().ContainSingle().Subject;
        group.Rows.Select(r => r.Name).Should().Equal("Ed", "Eve");
        TeamWeekRowDto eve = group.Rows[1];
        eve.DaySeconds.Should().Equal(8 * 3600, 27000, 0, 0, 0, 0, 0);
        eve.TotalSeconds.Should().Be(55800);
        eve.Status.Should().Be(WeekStatus.Draft);
        group.Rows[0].Status.Should().Be(WeekStatus.NotStarted);
        group.Rows[0].TotalSeconds.Should().Be(0);
    }

    [Fact]
    public async Task Includes_a_deactivated_employee_only_in_weeks_with_a_timesheet()
    {
        PersonRow rob = _world.Person("Rob", Role.Employee, _world.Warehouse.Id, UserStatus.Deactivated);
        _world.Timesheet(rob, new DateOnly(2026, 10, 12));

        TeamWeekDto withTimesheet = await Grid(new DateOnly(2026, 10, 12));
        TeamWeekDto without = await Grid(ReportingWorld.ThisWeek);

        withTimesheet.Groups[0].Rows.Select(r => r.Name).Should().Contain("Rob");
        without.Groups[0].Rows.Select(r => r.Name).Should().NotContain("Rob");
    }

    [Fact]
    public async Task Shows_a_moved_employee_under_the_new_team_with_the_whole_week()
    {
        _world.Timesheet(_world.Eve, ReportingWorld.ThisWeek, entries: [("2026-10-26 08:00", "2026-10-26 16:00")]);
        _world.Move(_world.Eve, _world.Office.Id);

        TeamWeekDto sams = await Grid(ReportingWorld.ThisWeek);
        _world.SignIn(_world.Sue);
        TeamWeekDto sues = await Grid(ReportingWorld.ThisWeek);

        sams.Groups[0].Rows.Select(r => r.Name).Should().NotContain("Eve");
        sues.Groups[0].Rows.Single(r => r.Name == "Eve").TotalSeconds.Should().Be(8 * 3600);
    }

    [Fact]
    public async Task Shows_the_running_total_of_an_open_entry()
    {
        _world.Timesheet(_world.Eve, ReportingWorld.ThisWeek, entries: [("2026-10-30 08:00", null)]);

        TeamWeekDto grid = await Grid(ReportingWorld.ThisWeek);

        TeamWeekRowDto eve = grid.Groups[0].Rows.Single(r => r.Name == "Eve");
        eve.DaySeconds[4].Should().Be(2 * 3600);
        eve.OpenDay.Should().Be(4);
    }

    [Fact]
    public async Task Groups_every_team_with_a_summary_and_the_supervisors_for_admins()
    {
        _world.Timesheet(_world.Eve, ReportingWorld.ThisWeek, TimesheetStatus.Submitted);
        _world.SignIn(_world.Ada);

        TeamWeekDto grid = await Grid(ReportingWorld.ThisWeek);

        grid.Groups.Select(g => g.TeamName).Should().Equal("Office", "Warehouse", null);
        grid.Groups[1].SupervisorName.Should().Be("Sam");
        grid.Groups[1].Summary.Should().Be(new WeekSummaryDto(NotStarted: 1, Draft: 0, Submitted: 1, Approved: 0));
        grid.Groups[2].IsSupervisors.Should().BeTrue();
        grid.Groups[2].Rows.Select(r => r.Name).Should().Equal("Sam", "Sue");
    }

    [Fact]
    public async Task Filters_by_team_for_admins()
    {
        _world.SignIn(_world.Ada);

        TeamWeekDto grid = await Grid(ReportingWorld.ThisWeek, _world.Office.Id);

        grid.Groups.Should().ContainSingle().Which.Rows.Select(r => r.Name).Should().Equal("Olga");
    }

    [Fact]
    public async Task Forbids_a_supervisor_another_team_and_employees_any_team()
    {
        var other = () => Grid(ReportingWorld.ThisWeek, _world.Office.Id);
        await other.Should().ThrowAsync<ForbiddenException>();

        _world.SignIn(_world.Eve);
        var employee = () => Grid(ReportingWorld.ThisWeek, _world.Warehouse.Id);
        await employee.Should().ThrowAsync<ForbiddenException>();
    }

    private Task<TeamWeekDto> Grid(DateOnly week, Guid? team = null) =>
        new GetTeamWeekHandler(_world.CurrentUser, _world.Reads, _world.Clock).HandleAsync(new GetTeamWeekQuery(week, team), CancellationToken.None);
}
