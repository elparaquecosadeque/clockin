using AwesomeAssertions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Application.Timesheets.Queries;
using ClockIn.Application.Timesheets.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Timesheets;

/// <summary>clock-screen.feature: what the clock screen needs, computed in company time.</summary>
public sealed class GetClockStatusHandlerTests
{
    private static readonly PunchSource Phone = new("203.0.113.7", "Mobile Safari");
    private static readonly Week ThisWeek = new(new DateOnly(2026, 10, 26));

    private readonly UserId _eve = UserId.New();
    private readonly CompanyClock _clock = new("2026-10-30 10:00");
    private readonly ITimesheetRepository _timesheetsRepository = Substitute.For<ITimesheetRepository>();
    private readonly GetClockStatusHandler _handler;

    public GetClockStatusHandlerTests()
    {
        ICurrentUser currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(_eve);
        _handler = new GetClockStatusHandler(currentUser, _timesheetsRepository, _clock);
    }

    [Fact]
    public async Task Reports_nothing_open_and_zero_totals_when_there_are_no_entries()
    {
        ClockStatusDto status = await _handler.HandleAsync(new GetClockStatusQuery(), CancellationToken.None);

        status.OpenEntry.Should().BeNull();
        status.TodayEntries.Should().BeEmpty();
        status.ClosedTodaySeconds.Should().Be(0);
        status.ClosedWeekSeconds.Should().Be(0);
        status.TimeZone.Should().Be("America/Bogota");
    }

    [Fact]
    public async Task Counts_closed_entries_and_flags_an_open_entry_that_started_today()
    {
        var timesheet = Timesheet.Start(_eve, ThisWeek);
        Punch(timesheet, "2026-10-29 08:00", "2026-10-29 10:00");
        Punch(timesheet, "2026-10-30 06:00", "2026-10-30 07:00");
        timesheet.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);
        HasOpen(timesheet);

        ClockStatusDto status = await _handler.HandleAsync(new GetClockStatusQuery(), CancellationToken.None);

        status.OpenEntry!.Start.Should().Be(CompanyClock.At("2026-10-30 08:00"));
        status.OpenEntryStartedToday.Should().BeTrue();
        status.OpenEntryStartedThisWeek.Should().BeTrue();
        status.ClosedTodaySeconds.Should().Be(3600);
        status.ClosedWeekSeconds.Should().Be(3 * 3600);
        status.TodayEntries.Select(e => e.Start).Should().Equal(CompanyClock.At("2026-10-30 06:00"), CompanyClock.At("2026-10-30 08:00"));
    }

    [Fact]
    public async Task Leaves_an_entry_open_since_last_week_out_of_this_weeks_totals()
    {
        var lastWeek = Timesheet.Start(_eve, new Week(new DateOnly(2026, 10, 19)));
        lastWeek.ClockIn(CompanyClock.At("2026-10-25 22:00"), Phone, CompanyClock.Zone);
        HasOpen(lastWeek);
        var thisWeek = Timesheet.Start(_eve, ThisWeek);
        Punch(thisWeek, "2026-10-27 08:00", "2026-10-27 09:30");
        _timesheetsRepository.FindAsync(_eve, ThisWeek, Arg.Any<CancellationToken>()).Returns(thisWeek);

        ClockStatusDto status = await _handler.HandleAsync(new GetClockStatusQuery(), CancellationToken.None);

        status.OpenEntry.Should().NotBeNull();
        status.OpenEntryStartedToday.Should().BeFalse();
        status.OpenEntryStartedThisWeek.Should().BeFalse();
        status.ClosedWeekSeconds.Should().Be(5400);
    }

    [Fact]
    public async Task Uses_the_company_day_when_utc_is_already_tomorrow()
    {
        _clock.Set("2026-10-30 21:00");
        var timesheet = Timesheet.Start(_eve, ThisWeek);
        Punch(timesheet, "2026-10-30 19:00", "2026-10-30 20:00");
        _timesheetsRepository.FindAsync(_eve, ThisWeek, Arg.Any<CancellationToken>()).Returns(timesheet);

        ClockStatusDto status = await _handler.HandleAsync(new GetClockStatusQuery(), CancellationToken.None);

        status.ClosedTodaySeconds.Should().Be(3600);
    }

    private static void Punch(Timesheet timesheet, string start, string end)
    {
        timesheet.ClockIn(CompanyClock.At(start), Phone, CompanyClock.Zone);
        timesheet.ClockOut(CompanyClock.At(end), Phone);
    }

    private void HasOpen(Timesheet timesheet)
    {
        _timesheetsRepository.FindWithOpenEntryAsync(_eve, Arg.Any<CancellationToken>()).Returns(timesheet);
        _timesheetsRepository.FindAsync(_eve, timesheet.Week, Arg.Any<CancellationToken>()).Returns(timesheet);
    }
}
