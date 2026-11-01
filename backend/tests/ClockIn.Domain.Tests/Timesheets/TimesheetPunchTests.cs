using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;

namespace ClockIn.Domain.Tests.Timesheets;

/// <summary>clock-in.feature and clock-out.feature: the rules the Timesheet aggregate owns.</summary>
public sealed class TimesheetPunchTests
{
    private static readonly PunchSource Phone = new("203.0.113.7", "Mobile Safari");
    private static readonly PunchSource Laptop = new("198.51.100.4", "Firefox");

    [Fact]
    public void Opens_an_entry_at_the_given_time_when_clocking_in()
    {
        var timesheet = NewTimesheetFor("2026-10-30 08:00");

        timesheet.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);

        var entry = timesheet.Entries.Should().ContainSingle().Subject;
        entry.Start.Should().Be(CompanyClock.At("2026-10-30 08:00"));
        entry.End.Should().BeNull();
        entry.ClockInSource.Should().Be(Phone);
        entry.IsManuallyChanged.Should().BeFalse();
        timesheet.OpenEntry.Should().Be(entry);
    }

    [Fact]
    public void Rejects_a_second_clock_in_when_an_entry_is_open()
    {
        var timesheet = NewTimesheetFor("2026-10-30 08:00");
        timesheet.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);

        var act = () => timesheet.ClockIn(CompanyClock.At("2026-10-30 09:00"), Phone, CompanyClock.Zone);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("EntryAlreadyOpen");
    }

    [Fact]
    public void Allows_clocking_in_at_the_second_the_last_entry_ended()
    {
        var timesheet = NewTimesheetFor("2026-10-30 08:00");
        timesheet.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);
        timesheet.ClockOut(CompanyClock.At("2026-10-30 09:00"), Phone);

        timesheet.ClockIn(CompanyClock.At("2026-10-30 09:00"), Phone, CompanyClock.Zone);

        timesheet.OpenEntry!.Start.Should().Be(CompanyClock.At("2026-10-30 09:00"));
    }

    [Fact]
    public void Rejects_a_clock_in_outside_its_week()
    {
        var timesheet = NewTimesheetFor("2026-10-30 08:00");

        var act = () => timesheet.ClockIn(CompanyClock.At("2026-11-02 00:00"), Phone, CompanyClock.Zone);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Closes_the_open_entry_and_records_where_from_when_clocking_out()
    {
        var timesheet = NewTimesheetFor("2026-10-30 08:00");
        timesheet.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);

        timesheet.ClockOut(CompanyClock.At("2026-10-30 16:30"), Laptop);

        var entry = timesheet.Entries.Single();
        entry.End.Should().Be(CompanyClock.At("2026-10-30 16:30"));
        entry.ClockOutSource.Should().Be(Laptop);
        timesheet.OpenEntry.Should().BeNull();
    }

    [Fact]
    public void Rejects_a_clock_out_when_the_timesheet_is_not_a_draft()
    {
        // Unreachable through the other rules (a week with an open entry can't be submitted); this guards them.
        var open = TimeEntry.Restore(
            TimeEntryId.New(), CompanyClock.At("2026-10-30 08:00"), null, Phone, null, ManualChange.None, null, null, null, null);
        var submitted = Timesheet.Restore(
            TimesheetId.New(), UserId.New(), new Week(new DateOnly(2026, 10, 26)), TimesheetStatus.Submitted, 1, null, null, [open], []);

        var act = () => submitted.ClockOut(CompanyClock.At("2026-10-30 16:30"), Phone);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("TimesheetNotEditable");
        open.End.Should().BeNull();
    }

    [Fact]
    public void Drops_the_zone_of_a_link_local_ipv6_address()
    {
        // The zone names the server's network interface; with it, an address can outgrow the 45-character column.
        new PunchSource("fe80::1ff:fe23:4567:890a%ethernet-adapter-12", "Safari").IpAddress.Should().Be("fe80::1ff:fe23:4567:890a");
    }

    [Fact]
    public void Rejects_a_clock_out_when_no_entry_is_open()
    {
        var timesheet = NewTimesheetFor("2026-10-30 08:00");

        var act = () => timesheet.ClockOut(CompanyClock.At("2026-10-30 16:30"), Phone);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("NoOpenEntry");
    }

    [Fact]
    public void Keeps_an_entry_in_the_week_it_started_when_it_ends_after_sunday_midnight()
    {
        var timesheet = NewTimesheetFor("2026-11-01 22:00");
        timesheet.ClockIn(CompanyClock.At("2026-11-01 22:00"), Phone, CompanyClock.Zone);

        timesheet.ClockOut(CompanyClock.At("2026-11-02 06:00"), Phone);

        timesheet.Week.Start.Should().Be(new DateOnly(2026, 10, 26));
        timesheet.Entries.Single().Duration.Should().Be(TimeSpan.FromHours(8));
    }

    [Theory]
    [InlineData("2026-10-29 18:00:00", "EntryHasNoLength")]
    [InlineData("2026-10-29 18:00:01", null)]
    [InlineData("2026-10-30 10:00:00", null)]
    [InlineData("2026-10-30 10:00:01", "EntryTooLong")]
    public void Checks_the_length_when_clocking_out(string clockOut, string? rule)
    {
        var timesheet = NewTimesheetFor("2026-10-29 18:00");
        timesheet.ClockIn(CompanyClock.At("2026-10-29 18:00"), Phone, CompanyClock.Zone);

        var act = () => timesheet.ClockOut(CompanyClock.At(clockOut), Phone);

        if (rule is null)
        {
            act.Should().NotThrow();
            timesheet.OpenEntry.Should().BeNull();
        }
        else
        {
            act.Should().Throw<DomainException>().Which.Code.Should().Be(rule);
            timesheet.OpenEntry.Should().NotBeNull("a rejected clock-out leaves the entry open to be fixed by hand");
        }
    }

    private static Timesheet NewTimesheetFor(string companyTime) =>
        Timesheet.Start(UserId.New(), Week.Containing(CompanyClock.At(companyTime), CompanyClock.Zone));
}
