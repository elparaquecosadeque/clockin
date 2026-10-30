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

/// <summary>clock-in.feature, as the use case sees it.</summary>
public sealed class ClockInHandlerTests
{
    private static readonly PunchSource Phone = new("203.0.113.7", "Mobile Safari");

    private readonly UserId _eve = UserId.New();
    private readonly CompanyClock _clock = new("2026-10-30 08:00");
    private readonly ITimesheetRepository _timesheets = Substitute.For<ITimesheetRepository>();
    private readonly ClockInHandler _handler;

    public ClockInHandlerTests()
    {
        ICurrentUser currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(_eve);
        _handler = new ClockInHandler(currentUser, _timesheets, _clock);
    }

    [Fact]
    public async Task Creates_a_draft_timesheet_with_an_open_entry_when_it_is_the_first_punch_of_the_week()
    {
        var result = await _handler.HandleAsync(new ClockInCommand("203.0.113.7", "Mobile Safari"), CancellationToken.None);

        await _timesheets.Received(1).SaveAsync(
            Arg.Is<Timesheet>(t =>
                t.OwnerId == _eve
                && t.Week == new Week(new DateOnly(2026, 10, 26))
                && t.Status == TimesheetStatus.Draft
                && t.OpenEntry!.ClockInSource == Phone),
            Arg.Any<CancellationToken>());
        result.Start.Should().Be(CompanyClock.At("2026-10-30 08:00"));
        result.End.Should().BeNull();
    }

    [Fact]
    public async Task Adds_the_entry_to_the_existing_timesheet_when_the_week_already_has_one()
    {
        Timesheet existing = TimesheetWithClosedEntry("2026-10-30 06:00", "2026-10-30 07:00");
        _timesheets.FindAsync(_eve, existing.Week, Arg.Any<CancellationToken>()).Returns(existing);

        await _handler.HandleAsync(new ClockInCommand("203.0.113.7", "Mobile Safari"), CancellationToken.None);

        existing.Entries.Should().HaveCount(2);
        existing.OpenEntry!.Start.Should().Be(CompanyClock.At("2026-10-30 08:00"));
        await _timesheets.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_the_punch_when_an_entry_from_last_week_is_still_open()
    {
        var lastWeek = Timesheet.Start(_eve, new Week(new DateOnly(2026, 10, 19)));
        lastWeek.ClockIn(CompanyClock.At("2026-10-25 22:00"), Phone, CompanyClock.Zone);
        _timesheets.FindWithOpenEntryAsync(_eve, Arg.Any<CancellationToken>()).Returns(lastWeek);

        var act = () => _handler.HandleAsync(new ClockInCommand("203.0.113.7", "Mobile Safari"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("EntryAlreadyOpen");
        await _timesheets.DidNotReceiveWithAnyArgs().SaveAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Files_the_entry_under_the_company_week_when_utc_is_already_monday()
    {
        _clock.Set("2026-11-01 23:59:59");

        await _handler.HandleAsync(new ClockInCommand("203.0.113.7", "Mobile Safari"), CancellationToken.None);

        await _timesheets.Received(1).SaveAsync(
            Arg.Is<Timesheet>(t => t.Week == new Week(new DateOnly(2026, 10, 26))),
            Arg.Any<CancellationToken>());
    }

    private Timesheet TimesheetWithClosedEntry(string start, string end)
    {
        var timesheet = Timesheet.Start(_eve, Week.Containing(CompanyClock.At(start), CompanyClock.Zone));
        timesheet.ClockIn(CompanyClock.At(start), Phone, CompanyClock.Zone);
        timesheet.ClockOut(CompanyClock.At(end), Phone);
        return timesheet;
    }
}
