using AwesomeAssertions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Commands;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Timesheets;

/// <summary>clock-out.feature, as the use case sees it.</summary>
public sealed class ClockOutHandlerTests
{
    private static readonly PunchSource Phone = new("203.0.113.7", "Mobile Safari");
    private static readonly PunchSource Laptop = new("198.51.100.4", "Firefox");

    private readonly UserId _eve = UserId.New();
    private readonly CompanyClock _clock = new("2026-10-30 16:30");
    private readonly ITimesheetRepository _timesheets = Substitute.For<ITimesheetRepository>();
    private readonly ClockOutHandler _handler;

    public ClockOutHandlerTests()
    {
        ICurrentUser currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(_eve);
        _handler = new ClockOutHandler(currentUser, _timesheets, _clock);
    }

    [Fact]
    public async Task Closes_the_open_entry_at_the_current_time()
    {
        Timesheet timesheet = OpenSince("2026-10-30 08:00");

        var result = await _handler.HandleAsync(new ClockOutCommand("198.51.100.4", "Firefox"), CancellationToken.None);

        result.End.Should().Be(CompanyClock.At("2026-10-30 16:30"));
        timesheet.OpenEntry.Should().BeNull();
        timesheet.Entries.Single().ClockOutSource.Should().Be(Laptop);
        await _timesheets.Received(1).SaveAsync(timesheet, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Closes_an_entry_left_open_from_last_week_in_that_week()
    {
        Timesheet lastWeek = OpenSince("2026-10-25 20:00");
        _clock.Set("2026-10-26 04:00");

        await _handler.HandleAsync(new ClockOutCommand("198.51.100.4", "Firefox"), CancellationToken.None);

        lastWeek.Week.Should().Be(new Week(new DateOnly(2026, 10, 19)));
        lastWeek.Entries.Single().Duration.Should().Be(TimeSpan.FromHours(8));
        await _timesheets.Received(1).SaveAsync(lastWeek, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_the_punch_when_no_entry_is_open()
    {
        var act = () => _handler.HandleAsync(new ClockOutCommand("198.51.100.4", "Firefox"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("NoOpenEntry");
    }

    [Fact]
    public async Task Leaves_the_entry_open_and_unsaved_when_it_would_be_too_long()
    {
        Timesheet timesheet = OpenSince("2026-10-29 08:00");
        _clock.Set("2026-10-30 10:00");

        var act = () => _handler.HandleAsync(new ClockOutCommand("198.51.100.4", "Firefox"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("EntryTooLong");
        timesheet.OpenEntry.Should().NotBeNull();
        await _timesheets.DidNotReceiveWithAnyArgs().SaveAsync(default!, TestContext.Current.CancellationToken);
    }

    private Timesheet OpenSince(string start)
    {
        var timesheet = Timesheet.Start(_eve, Week.Containing(CompanyClock.At(start), CompanyClock.Zone));
        timesheet.ClockIn(CompanyClock.At(start), Phone, CompanyClock.Zone);
        _timesheets.FindWithOpenEntryAsync(_eve, Arg.Any<CancellationToken>()).Returns(timesheet);
        return timesheet;
    }
}
