using AwesomeAssertions;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Timesheets.Commands;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Timesheets;

/// <summary>add-manual-entry, edit-entry and delete-entry features, as the use cases see them.</summary>
public sealed class EntryChangeHandlerTests
{
    private static readonly Week LastWeek = TimesheetWorld.WeekOf(2026, 10, 19);

    private readonly TimesheetWorld _world = new();

    [Fact]
    public async Task Creates_a_draft_timesheet_for_a_past_week_that_had_none()
    {
        await Add(null, "2026-10-14 08:00", "2026-10-14 12:00");

        await _world.TimesheetsRepository.Received(1).SaveOrUpdateAsync(
            Arg.Is<Timesheet>(t => t.Week == TimesheetWorld.WeekOf(2026, 10, 12) && t.Entries.Single().ManualChange == ManualChange.Added),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checks_overlaps_against_the_open_entry_of_another_week()
    {
        var older = Timesheet.Start(_world.Eve.Id, LastWeek);
        older.ClockIn(CompanyClock.At("2026-10-25 20:00"), TimesheetWorld.Phone, CompanyClock.Zone);
        _world.Add(older);
        _world.Clock.Set("2026-10-26 10:00");

        var act = () => Add(null, "2026-10-26 01:00", "2026-10-26 03:00");

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("EntriesOverlap");
    }

    [Fact]
    public async Task Locks_the_owner_before_reading_the_neighbouring_weeks()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek);

        await Add(null, "2026-10-29 08:00", "2026-10-29 12:00");
        await Edit(week.Entries.Single().Id, "2026-10-20 07:30", "2026-10-20 12:00", week.Version);

        // Two writes for Eve in neighbouring weeks would each miss the other's entry.
        Received.InOrder(() =>
        {
            _world.TimesheetsRepository.LockOwnerAsync(_world.Eve.Id, Arg.Any<CancellationToken>());
            _world.TimesheetsRepository.FindWithOpenEntryAsync(_world.Eve.Id, Arg.Any<CancellationToken>());
            _world.TimesheetsRepository.LockOwnerAsync(_world.Eve.Id, Arg.Any<CancellationToken>());
            _world.TimesheetsRepository.FindWithOpenEntryAsync(_world.Eve.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Lets_an_admin_add_to_anyones_draft_flagged_as_the_admin()
    {
        _world.SignIn(_world.Ada);

        await Add(_world.Eve.Id, "2026-10-29 08:00", "2026-10-29 12:00", "badge reader down");

        await _world.TimesheetsRepository.Received(1).SaveOrUpdateAsync(
            Arg.Is<Timesheet>(t => t.OwnerId == _world.Eve.Id && t.Entries.Single().ChangedBy == _world.Ada.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Forbids_a_supervisor_from_adding_to_a_team_member()
    {
        _world.SignIn(_world.Sam);

        var act = () => Add(_world.Eve.Id, "2026-10-29 08:00", "2026-10-29 12:00");

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Edits_an_entry_and_keeps_the_punched_values()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek);
        TimeEntry entry = week.Entries.Single();

        await Edit(entry.Id, "2026-10-20 07:30", "2026-10-20 12:00", week.Version);

        entry.Start.Should().Be(CompanyClock.At("2026-10-20 07:30"));
        entry.PunchedStart.Should().Be(CompanyClock.At("2026-10-20 08:00"));
        await _world.TimesheetsRepository.Received(1).SaveOrUpdateAsync(week, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Ed")]
    [InlineData("Sam")]
    public async Task Forbids_editing_someone_elses_entry(string actor)
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek);
        _world.SignIn(actor == "Ed" ? _world.Ed : _world.Sam);

        var act = () => Edit(week.Entries.Single().Id, "2026-10-20 07:30", "2026-10-20 12:00", week.Version);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Rejects_an_edit_based_on_a_stale_version()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek);

        var act = () => Edit(week.Entries.Single().Id, "2026-10-20 07:30", "2026-10-20 12:00", week.Version + 1);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("ConcurrencyConflict");
    }

    [Fact]
    public async Task Reports_an_unknown_entry_as_not_found()
    {
        var act = () => Edit(TimeEntryId.New(), "2026-10-20 07:30", "2026-10-20 12:00", 0);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deletes_an_entry_with_a_note()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek);

        await new DeleteEntryHandler(_world.CurrentUser, _world.TimesheetAccess, _world.TimesheetsRepository, _world.Clock)
            .HandleAsync(new DeleteEntryCommand(week.Entries.Single().Id.Value, "duplicate", week.Version), CancellationToken.None);

        week.Entries.Should().BeEmpty();
        week.History.Single().Kind.Should().Be(TimesheetEventKind.EntryDeleted);
    }

    private Task<Guid> Add(UserId? owner, string start, string end, string note = "forgot") =>
        new AddEntryHandler(_world.CurrentUser, _world.TimesheetAccess, _world.TimesheetsRepository, _world.Clock)
            .HandleAsync(new AddEntryCommand(owner?.Value, CompanyClock.At(start), CompanyClock.At(end), note, "203.0.113.7", "Safari"), CancellationToken.None);

    private Task Edit(TimeEntryId entry, string start, string end, int version) =>
        new EditEntryHandler(_world.CurrentUser, _world.TimesheetAccess, _world.TimesheetsRepository, _world.Clock)
            .HandleAsync(new EditEntryCommand(entry.Value, CompanyClock.At(start), CompanyClock.At(end), "note", version), CancellationToken.None);
}
