using AwesomeAssertions;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Reporting.Queries;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;

namespace ClockIn.Application.Tests.Reporting;

/// <summary>clocked-in-now.feature, exceptions.feature and to-do-badges.feature.</summary>
public sealed class MonitoringHandlerTests
{
    private readonly ReportingWorld _world = new();

    [Fact]
    public async Task Shows_a_supervisor_who_of_the_team_is_clocked_in()
    {
        ClockIn("Eve", "2026-10-30 08:00");
        ClockIn("Olga", "2026-10-30 09:30");
        ClockIn("Sam", "2026-10-30 07:00");

        IReadOnlyList<ClockedInDto> forSam = await ClockedIn();
        _world.SignIn(_world.Ada);
        IReadOnlyList<ClockedInDto> forAda = await ClockedIn();

        forSam.Should().ContainSingle().Which.Should().Be(
            new ClockedInDto(_world.Eve.Id, "Eve", "Warehouse", CompanyClock.At("2026-10-30 08:00"), 2 * 3600, false));
        forAda.Select(c => c.Name).Should().Equal("Sam", "Eve", "Olga");
        forAda[0].TeamName.Should().BeNull();
    }

    [Fact]
    public async Task Flags_an_entry_open_for_more_than_16_hours()
    {
        ClockIn("Ed", "2026-10-29 08:00");

        (await ClockedIn()).Single().OverLimit.Should().BeTrue();
    }

    [Fact]
    public async Task Forbids_employees_the_reports()
    {
        _world.SignIn(_world.Eve);

        var clockedIn = () => ClockedIn();
        var exceptions = () => Exceptions();

        await clockedIn.Should().ThrowAsync<ForbiddenException>();
        await exceptions.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Lists_overdue_drafts_of_ended_weeks_only()
    {
        _world.Timesheet(_world.Eve, ReportingWorld.LastWeek);
        _world.Timesheet(_world.Ed, ReportingWorld.LastWeek, TimesheetStatus.Submitted);
        _world.Timesheet(_world.Eve, ReportingWorld.ThisWeek);
        _world.Timesheet(_world.Eve, new DateOnly(2026, 10, 12), returnComment: "fix Tuesday");
        _world.Timesheet(_world.Olga, ReportingWorld.LastWeek);

        IReadOnlyList<ExceptionDto> found = await Exceptions();

        found.Should().OnlyContain(e => e.Kind == ExceptionKind.OverdueDraft && e.Name == "Eve");
        found.Select(e => e.WeekStart).Should().Equal(ReportingWorld.LastWeek, new DateOnly(2026, 10, 12));
    }

    [Theory]
    [InlineData("2026-10-20 18:00", false)]
    [InlineData("2026-10-20 18:01", true)]
    public async Task Lists_entries_longer_than_10_hours(string end, bool listed)
    {
        _world.Timesheet(_world.Eve, ReportingWorld.LastWeek, TimesheetStatus.Submitted, [("2026-10-20 08:00", end)]);

        IReadOnlyList<ExceptionDto> found = await Exceptions();

        found.Any(e => e.Kind == ExceptionKind.LongEntry).Should().Be(listed);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task Lists_timesheets_with_many_manual_changes(int count, bool listed)
    {
        _world.Timesheet(_world.Eve, ReportingWorld.LastWeek, TimesheetStatus.Submitted, manualChanges: count);

        IReadOnlyList<ExceptionDto> found = await Exceptions();

        found.Any(e => e.Kind == ExceptionKind.ManyManualChanges).Should().Be(listed);
    }

    [Fact]
    public async Task Shows_admins_every_team_and_the_supervisors()
    {
        _world.Timesheet(_world.Sam, ReportingWorld.LastWeek);
        _world.SignIn(_world.Ada);

        (await Exceptions()).Should().ContainSingle().Which.Name.Should().Be("Sam");
    }

    [Fact]
    public async Task Counts_an_employees_to_dos()
    {
        ClockIn("Eve", "2026-10-30 08:00");
        _world.Timesheet(_world.Eve, new DateOnly(2026, 10, 12), returnComment: "Tuesday is missing");
        _world.Timesheet(_world.Eve, ReportingWorld.LastWeek);
        TimesheetRow approved = _world.Timesheet(_world.Eve, new DateOnly(2026, 10, 5), TimesheetStatus.Approved);
        _world.Correction(approved, CorrectionStatus.Declined, "2026-10-27 10:00");
        _world.Correction(approved, CorrectionStatus.Declined, "2026-10-22 10:00");
        _world.SignIn(_world.Eve);

        ToDosDto todos = await ToDos();

        todos.Should().BeEquivalentTo(new ToDosDto(
            CompanyClock.At("2026-10-30 08:00"), [new DateOnly(2026, 10, 12)], [ReportingWorld.LastWeek], 1, 0, 0));
    }

    [Fact]
    public async Task Counts_a_supervisors_queue_from_the_team_and_an_admins_from_the_supervisors()
    {
        _world.Timesheet(_world.Eve, ReportingWorld.LastWeek, TimesheetStatus.Submitted);
        TimesheetRow ed = _world.Timesheet(_world.Ed, ReportingWorld.LastWeek, TimesheetStatus.Submitted);
        _world.Correction(ed);
        _world.Timesheet(_world.Sam, ReportingWorld.LastWeek, TimesheetStatus.Submitted);
        _world.Correction(_world.Timesheet(_world.Sue, ReportingWorld.LastWeek, TimesheetStatus.Approved));

        ToDosDto sam = await ToDos();
        _world.SignIn(_world.Ada);
        ToDosDto ada = await ToDos();

        (sam.TimesheetsToReview, sam.CorrectionsToDecide).Should().Be((2, 1));
        sam.UnsubmittedWeeks.Should().BeEmpty();
        (ada.TimesheetsToReview, ada.CorrectionsToDecide).Should().Be((1, 1));
    }

    [Fact]
    public async Task Shows_nothing_when_everything_is_done()
    {
        _world.SignIn(_world.Ed);

        (await ToDos()).Should().BeEquivalentTo(new ToDosDto(null, [], [], 0, 0, 0));
    }

    private void ClockIn(string name, string at)
    {
        PersonRow person = name switch { "Eve" => _world.Eve, "Ed" => _world.Ed, "Olga" => _world.Olga, _ => _world.Sam };
        DateOnly monday = WorkedWeek(at);
        _world.Timesheet(person, monday, entries: [(at, null)]);
    }

    private static DateOnly WorkedWeek(string at)
    {
        var day = DateOnly.FromDateTime(DateTime.Parse(at, System.Globalization.CultureInfo.InvariantCulture));
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }

    private Task<IReadOnlyList<ClockedInDto>> ClockedIn() =>
        new GetClockedInHandler(_world.CurrentUser, _world.Reads, _world.Clock).HandleAsync(new GetClockedInQuery(), CancellationToken.None);

    private Task<IReadOnlyList<ExceptionDto>> Exceptions() =>
        new GetExceptionsHandler(_world.CurrentUser, _world.Reads, _world.Clock).HandleAsync(new GetExceptionsQuery(), CancellationToken.None);

    private Task<ToDosDto> ToDos() =>
        new GetToDosHandler(_world.CurrentUser, _world.Reads, _world.Clock).HandleAsync(new GetToDosQuery(), CancellationToken.None);
}
