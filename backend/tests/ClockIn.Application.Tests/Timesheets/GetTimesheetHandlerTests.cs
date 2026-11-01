using AwesomeAssertions;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Timesheets.Queries;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;

namespace ClockIn.Application.Tests.Timesheets;

/// <summary>view-timesheet.feature, as the use case sees it.</summary>
public sealed class GetTimesheetHandlerTests
{
    private static readonly Week LastWeek = TimesheetWorld.WeekOf(2026, 10, 19);
    private static readonly Week ThisWeek = TimesheetWorld.WeekOf(2026, 10, 26);

    private readonly TimesheetWorld _world = new();

    [Fact]
    public async Task Counts_hours_on_the_day_an_entry_started()
    {
        var week = Timesheet.Start(_world.Eve.Id, LastWeek);
        week.AddEntry(
            _world.Eve.Id, CompanyClock.At("2026-10-20 22:00"), CompanyClock.At("2026-10-21 02:00"), "night shift",
            TimesheetWorld.Phone, _world.Clock.GetUtcNow(), CompanyClock.Zone, []);
        _world.Add(week);

        TimesheetDto result = await View(null, LastWeek);

        result.Days.Single(d => d.Date == new DateOnly(2026, 10, 20)).Seconds.Should().Be(4 * 3600);
        result.Days.Single(d => d.Date == new DateOnly(2026, 10, 21)).Seconds.Should().Be(0);
        result.TotalSeconds.Should().Be(4 * 3600);
        result.Entries.Single().ManualChange.Should().Be(ManualChange.Added);
        result.Entries.Single().ChangedByName.Should().Be("Eve");
        result.History.Single().ByOwner.Should().BeTrue();
    }

    [Fact]
    public async Task Includes_an_open_entry_up_to_now_in_the_running_total()
    {
        var week = Timesheet.Start(_world.Eve.Id, ThisWeek);
        week.ClockIn(CompanyClock.At("2026-10-30 08:00"), TimesheetWorld.Phone, CompanyClock.Zone);
        _world.Add(week);

        TimesheetDto result = await View(null, ThisWeek);

        DayTotalDto friday = result.Days.Single(d => d.Date == new DateOnly(2026, 10, 30));
        friday.Seconds.Should().Be(2 * 3600);
        friday.HasOpenEntry.Should().BeTrue();
        result.Can.Submit.Should().BeFalse("the week has not ended");
        result.Can.ChangeEntries.Should().BeTrue();
    }

    [Fact]
    public async Task Shows_a_week_without_a_timesheet_as_not_started()
    {
        TimesheetDto result = await View(null, LastWeek);

        result.Id.Should().BeNull();
        result.Status.Should().Be(WeekStatus.NotStarted);
        result.Days.Should().HaveCount(7);
        result.Can.Submit.Should().BeTrue();
        result.TeamName.Should().Be("Warehouse");
    }

    [Theory]
    [InlineData("Eve", true)]
    [InlineData("Sam", true)]
    [InlineData("Ada", true)]
    [InlineData("Ed", false)]
    [InlineData("Sue", false)]
    public async Task Controls_who_can_see_it(string viewer, bool allowed)
    {
        _world.Week(_world.Eve, LastWeek, TimesheetStatus.Submitted);
        _world.SignIn(viewer switch { "Eve" => _world.Eve, "Sam" => _world.Sam, "Ada" => _world.Ada, "Ed" => _world.Ed, _ => _world.Sue });

        var act = () => View(_world.Eve.Id, LastWeek);

        if (allowed)
        {
            (await act()).OwnerName.Should().Be("Eve");
        }
        else
        {
            await act.Should().ThrowAsync<ForbiddenException>();
        }
    }

    [Fact]
    public async Task Hides_the_timesheet_from_a_former_supervisor()
    {
        _world.Week(_world.Eve, LastWeek, TimesheetStatus.Submitted);
        _world.Move(_world.Eve, _world.Office);
        _world.SignIn(_world.Sam);

        var act = () => View(_world.Eve.Id, LastWeek);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Hides_the_timesheet_from_the_supervisor_of_an_archived_team()
    {
        _world.Week(_world.Eve, LastWeek, TimesheetStatus.Approved);
        _world.Warehouse.Archive();
        _world.SignIn(_world.Sam);

        var act = () => View(_world.Eve.Id, LastWeek);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Offers_the_approver_the_decisions_and_the_owner_a_correction_request()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Submitted);
        _world.SignIn(_world.Sam);

        TimesheetDto asSam = await View(_world.Eve.Id, LastWeek);
        _world.SignIn(_world.Eve);
        TimesheetDto asEve = await View(null, LastWeek);
        _world.PendingRequest(week);
        _world.SignIn(_world.Sam);
        TimesheetDto pending = await View(_world.Eve.Id, LastWeek);

        asSam.Can.Should().Be(new TimesheetActionsDto(false, false, true, true, false, false, false));
        asEve.Can.Should().Be(new TimesheetActionsDto(false, false, false, false, false, true, false));
        pending.Can.Should().Be(new TimesheetActionsDto(false, false, false, false, false, false, true));
        pending.LatestCorrection!.Status.Should().Be(CorrectionStatus.Pending);
    }

    private Task<TimesheetDto> View(UserId? owner, Week week) =>
        new GetTimesheetHandler(_world.CurrentUser, _world.TimesheetAccess, _world.UsersRepository, _world.TimesheetsRepository, _world.CorrectionsRepository, _world.Clock)
            .HandleAsync(new GetTimesheetQuery(owner?.Value, week.Start), CancellationToken.None);
}
