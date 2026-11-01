using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;

namespace ClockIn.Domain.Tests.Timesheets;

/// <summary>add-manual-entry.feature, edit-entry.feature and delete-entry.feature: the rules the Timesheet owns.</summary>
public sealed class TimesheetEntryChangeTests
{
    private static readonly PunchSource Phone = new("203.0.113.7", "Mobile Safari");
    private static readonly DateTimeOffset Now = CompanyClock.At("2026-10-30 10:00");

    private readonly UserId _eve = UserId.New();
    private readonly UserId _ada = UserId.New();
    private readonly Timesheet _week;

    public TimesheetEntryChangeTests() => _week = Timesheet.Start(_eve, new Week(new DateOnly(2026, 10, 26)));

    [Fact]
    public void Adds_a_flagged_entry_with_a_note_and_records_it()
    {
        TimeEntry entry = Add("2026-10-29 08:00", "2026-10-29 12:00", " forgot my phone ");

        entry.ManualChange.Should().Be(ManualChange.Added);
        entry.ChangedBy.Should().Be(_eve);
        entry.Note.Should().Be("forgot my phone");
        entry.IsManuallyChanged.Should().BeTrue();
        TimesheetEvent added = _week.History.Should().ContainSingle().Subject;
        added.Kind.Should().Be(TimesheetEventKind.EntryAdded);
        added.After!.Start.Should().Be(CompanyClock.At("2026-10-29 08:00"));
        added.Comment.Should().Be("forgot my phone");
    }

    [Fact]
    public void Flags_an_admin_addition_as_made_by_the_admin()
    {
        TimeEntry entry = _week.AddEntry(
            _ada, CompanyClock.At("2026-10-29 08:00"), CompanyClock.At("2026-10-29 12:00"), "badge reader down", Phone, Now, CompanyClock.Zone, []);

        entry.ChangedBy.Should().Be(_ada);
        _week.History.Single().ActorId.Should().Be(_ada);
    }

    [Theory]
    [InlineData("2026-10-30 08:00", "2026-10-30 10:00", null)]
    [InlineData("2026-10-30 08:00", "2026-10-30 10:00:01", "EntryInFuture")]
    [InlineData("2026-10-29 09:00", "2026-10-29 09:00", "EntryHasNoLength")]
    [InlineData("2026-10-29 09:00", "2026-10-29 08:00", "EntryHasNoLength")]
    [InlineData("2026-10-28 08:00", "2026-10-29 00:00", null)]
    [InlineData("2026-10-28 08:00", "2026-10-29 00:01", "EntryTooLong")]
    public void Checks_the_new_entry_against_now_and_its_length(string start, string end, string? code)
    {
        Action act = () => Add(start, end);

        AssertOutcome(act, code);
    }

    [Theory]
    [InlineData("08:00", "09:30", "EntriesOverlap")]
    [InlineData("11:30", "13:00", "EntriesOverlap")]
    [InlineData("10:00", "11:00", "EntriesOverlap")]
    [InlineData("08:00", "13:00", "EntriesOverlap")]
    [InlineData("09:00", "12:00", "EntriesOverlap")]
    [InlineData("08:00", "09:00", null)]
    [InlineData("12:00", "13:00", null)]
    public void Never_overlaps_but_may_touch(string start, string end, string? code)
    {
        Add("2026-10-29 09:00", "2026-10-29 12:00");

        Action act = () => Add($"2026-10-29 {start}", $"2026-10-29 {end}");

        AssertOutcome(act, code);
    }

    [Fact]
    public void Treats_an_open_entry_as_running_until_now()
    {
        _week.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);

        var act = () => Add("2026-10-30 09:00", "2026-10-30 09:30");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("EntriesOverlap");
    }

    [Fact]
    public void Checks_overlaps_across_a_week_boundary()
    {
        var lastWeek = Timesheet.Start(_eve, new Week(new DateOnly(2026, 10, 19)));
        lastWeek.AddEntry(_eve, CompanyClock.At("2026-10-25 22:00"), CompanyClock.At("2026-10-26 02:00"), "night", Phone, Now, CompanyClock.Zone, []);

        var act = () => _week.AddEntry(
            _eve, CompanyClock.At("2026-10-26 01:00"), CompanyClock.At("2026-10-26 03:00"), "night", Phone, Now, CompanyClock.Zone, [lastWeek]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("EntriesOverlap");
    }

    [Fact]
    public void Rejects_changes_to_a_timesheet_that_is_not_a_draft()
    {
        Timesheet submitted = SubmittedWeekWithEntry(out TimeEntryId entryId);

        AssertNotEditable(() => submitted.AddEntry(
            _eve, CompanyClock.At("2026-10-20 08:00"), CompanyClock.At("2026-10-20 12:00"), "note", Phone, Now, CompanyClock.Zone, []));
        AssertNotEditable(() => submitted.EditEntry(
            _eve, entryId, CompanyClock.At("2026-10-21 08:00"), CompanyClock.At("2026-10-21 12:00"), "note", Now, CompanyClock.Zone, []));
        AssertNotEditable(() => submitted.DeleteEntry(_eve, entryId, "note", Now));
    }

    [Fact]
    public void Edits_the_times_and_keeps_the_punched_values_forever()
    {
        TimeEntry entry = Punched("2026-10-29 08:02", "2026-10-29 17:00");

        Edit(entry, "2026-10-29 08:00", "2026-10-29 17:00", "badge was slow");
        Edit(entry, "2026-10-29 08:00", "2026-10-29 16:00");

        entry.Start.Should().Be(CompanyClock.At("2026-10-29 08:00"));
        entry.End.Should().Be(CompanyClock.At("2026-10-29 16:00"));
        entry.ManualChange.Should().Be(ManualChange.Edited);
        entry.PunchedStart.Should().Be(CompanyClock.At("2026-10-29 08:02"));
        entry.PunchedEnd.Should().Be(CompanyClock.At("2026-10-29 17:00"));
        TimesheetEvent[] edits = [.. _week.History.Where(e => e.Kind == TimesheetEventKind.EntryEdited)];
        edits.Should().HaveCount(2);
        edits[0].Before!.Start.Should().Be(CompanyClock.At("2026-10-29 08:02"));
        edits[0].After!.Start.Should().Be(CompanyClock.At("2026-10-29 08:00"));
        edits[0].Comment.Should().Be("badge was slow");
    }

    [Fact]
    public void Keeps_an_added_entry_flagged_as_added_when_edited()
    {
        TimeEntry entry = Add("2026-10-29 08:00", "2026-10-29 12:00");

        Edit(entry, "2026-10-29 08:00", "2026-10-29 11:00");

        entry.ManualChange.Should().Be(ManualChange.Added);
        entry.PunchedStart.Should().BeNull();
    }

    [Fact]
    public void Closes_a_forgotten_open_entry_by_setting_its_end()
    {
        _week.ClockIn(CompanyClock.At("2026-10-30 07:00"), Phone, CompanyClock.Zone);
        TimeEntry entry = _week.OpenEntry!;

        Edit(entry, "2026-10-30 07:00", "2026-10-30 09:30");

        entry.End.Should().Be(CompanyClock.At("2026-10-30 09:30"));
        _week.OpenEntry.Should().BeNull();
        entry.PunchedEnd.Should().BeNull();
    }

    [Fact]
    public void Moves_only_the_start_of_an_open_entry()
    {
        _week.ClockIn(CompanyClock.At("2026-10-30 09:15"), Phone, CompanyClock.Zone);
        TimeEntry entry = _week.OpenEntry!;

        _week.EditEntry(_eve, entry.Id, CompanyClock.At("2026-10-30 08:45"), null, "note", Now, CompanyClock.Zone, []);

        _week.OpenEntry.Should().Be(entry);
        entry.Start.Should().Be(CompanyClock.At("2026-10-30 08:45"));
    }

    [Theory]
    [InlineData("2026-10-29 18:00", null)]
    [InlineData("2026-10-29 17:59", "EntryTooLong")]
    public void Keeps_an_open_entry_within_16_hours_of_now(string start, string? code)
    {
        // Otherwise the entry could never be clocked out of, and neighbouring weeks would be checked too narrowly.
        _week.ClockIn(CompanyClock.At("2026-10-30 09:15"), Phone, CompanyClock.Zone);

        var act = () => _week.EditEntry(_eve, _week.OpenEntry!.Id, CompanyClock.At(start), null, "note", Now, CompanyClock.Zone, []);

        if (code is null)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
        }
    }

    [Fact]
    public void Rejects_reopening_a_closed_entry()
    {
        TimeEntry entry = Punched("2026-10-29 08:00", "2026-10-29 17:00");

        var act = () => _week.EditEntry(_eve, entry.Id, entry.Start, null, "note", Now, CompanyClock.Zone, []);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("EntryHasNoLength");
    }

    [Theory]
    [InlineData("2026-10-25 23:00", "2026-10-26 01:00", null)]
    [InlineData("2026-10-26 00:00", "2026-10-26 01:00", "EntryCannotChangeWeek")]
    [InlineData("2026-10-17 20:00", "2026-10-17 22:00", "EntryCannotChangeWeek")]
    public void Keeps_an_entry_in_the_week_it_started_in(string start, string end, string? code)
    {
        var lastWeek = Timesheet.Start(_eve, new Week(new DateOnly(2026, 10, 19)));
        TimeEntry entry = lastWeek.AddEntry(
            _eve, CompanyClock.At("2026-10-25 20:00"), CompanyClock.At("2026-10-25 22:00"), "note", Phone, Now, CompanyClock.Zone, []);

        var act = () => lastWeek.EditEntry(_eve, entry.Id, CompanyClock.At(start), CompanyClock.At(end), "note", Now, CompanyClock.Zone, []);

        AssertOutcome(act, code);
    }

    [Theory]
    [InlineData("2026-10-29 08:00", "2026-10-29 18:30", "EntriesOverlap")]
    [InlineData("2026-10-29 08:00", "2026-10-29 08:00", "EntryHasNoLength")]
    [InlineData("2026-10-29 08:00", "2026-10-30 00:01", "EntryTooLong")]
    [InlineData("2026-10-30 08:00", "2026-10-30 11:00", "EntryInFuture")]
    [InlineData("2026-10-29 08:30", "2026-10-29 16:30", null)]
    public void Applies_the_new_entry_rules_but_never_overlaps_itself(string start, string end, string? code)
    {
        TimeEntry entry = Punched("2026-10-29 08:02", "2026-10-29 17:00");
        Add("2026-10-29 18:00", "2026-10-29 20:00");

        var act = () => Edit(entry, start, end);

        AssertOutcome(act, code);
    }

    [Fact]
    public void Deletes_an_entry_and_keeps_a_snapshot_in_the_history()
    {
        TimeEntry entry = Punched("2026-10-29 08:02", "2026-10-29 12:00");
        Edit(entry, "2026-10-29 08:00", "2026-10-29 12:00");

        _week.DeleteEntry(_eve, entry.Id, " duplicate ", Now);

        _week.Entries.Should().BeEmpty();
        TimesheetEvent deleted = _week.History[^1];
        deleted.Kind.Should().Be(TimesheetEventKind.EntryDeleted);
        deleted.Before!.Should().Be(new EntrySnapshot(
            entry.Id, CompanyClock.At("2026-10-29 08:00"), CompanyClock.At("2026-10-29 12:00"),
            CompanyClock.At("2026-10-29 08:02"), CompanyClock.At("2026-10-29 12:00")));
        deleted.Comment.Should().Be("duplicate");
    }

    [Fact]
    public void Deletes_an_accidental_open_entry_so_the_owner_can_clock_in_again()
    {
        _week.ClockIn(CompanyClock.At("2026-10-30 09:59"), Phone, CompanyClock.Zone);

        _week.DeleteEntry(_eve, _week.OpenEntry!.Id, "tapped twice", Now);

        _week.OpenEntry.Should().BeNull();
        _week.ClockIn(Now, Phone, CompanyClock.Zone);
    }

    [Fact]
    public void Counts_the_distinct_entries_changed_by_hand_including_deleted_ones()
    {
        TimeEntry punched = Punched("2026-10-29 08:00", "2026-10-29 09:00");
        Edit(punched, "2026-10-29 08:00", "2026-10-29 09:30");
        Edit(punched, "2026-10-29 08:00", "2026-10-29 09:45");
        TimeEntry added = Add("2026-10-28 08:00", "2026-10-28 09:00");
        _week.DeleteEntry(_eve, added.Id, "dup", Now);

        _week.ManuallyChangedEntryCount.Should().Be(2);
    }

    private TimeEntry Add(string start, string end, string note = "note") =>
        _week.AddEntry(_eve, CompanyClock.At(start), CompanyClock.At(end), note, Phone, Now, CompanyClock.Zone, []);

    private void Edit(TimeEntry entry, string start, string end, string note = "note") =>
        _week.EditEntry(_eve, entry.Id, CompanyClock.At(start), CompanyClock.At(end), note, Now, CompanyClock.Zone, []);

    private TimeEntry Punched(string start, string end)
    {
        _week.ClockIn(CompanyClock.At(start), Phone, CompanyClock.Zone);
        TimeEntry entry = _week.OpenEntry!;
        _week.ClockOut(CompanyClock.At(end), Phone);
        return entry;
    }

    private Timesheet SubmittedWeekWithEntry(out TimeEntryId entryId)
    {
        var timesheet = Timesheet.Start(_eve, new Week(new DateOnly(2026, 10, 19)));
        entryId = timesheet.AddEntry(
            _eve, CompanyClock.At("2026-10-21 08:00"), CompanyClock.At("2026-10-21 12:00"), "note", Phone, Now, CompanyClock.Zone, []).Id;
        timesheet.Submit(_eve, Now, CompanyClock.Zone);
        return timesheet;
    }

    private static void AssertNotEditable(Action act) =>
        act.Should().Throw<DomainException>().Which.Code.Should().Be("TimesheetNotEditable");

    private static void AssertOutcome(Action act, string? code)
    {
        if (code is null)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
        }
    }
}
