using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;

namespace ClockIn.Domain.Tests.Timesheets;

/// <summary>submit, approve, return and reopen features, plus the timesheet side of correction requests.</summary>
public sealed class TimesheetWorkflowTests
{
    private static readonly PunchSource Phone = new("203.0.113.7", "Mobile Safari");
    private static readonly DateTimeOffset Monday = CompanyClock.At("2026-11-02 09:00");

    private readonly UserId _eve = UserId.New();
    private readonly UserId _sam = UserId.New();
    private readonly UserId _ada = UserId.New();
    private readonly TeamId _warehouse = TeamId.New();
    private readonly Timesheet _week;

    public TimesheetWorkflowTests() => _week = Timesheet.Start(_eve, new Week(new DateOnly(2026, 10, 26)));

    [Fact]
    public void Submits_an_ended_week_and_records_who_did_it()
    {
        _week.Submit(_eve, Monday, CompanyClock.Zone);

        _week.Status.Should().Be(TimesheetStatus.Submitted);
        _week.History.Should().ContainSingle().Which.Should().Be(
            new TimesheetEvent(TimesheetEventKind.Submitted, _eve, Monday, null, null, null));
    }

    [Theory]
    [InlineData("2026-11-01 23:59:59", "WeekNotEnded")]
    [InlineData("2026-11-02 00:00:00", null)]
    public void Submits_only_once_the_week_has_ended_in_company_time(string now, string? code)
    {
        var act = () => _week.Submit(_eve, CompanyClock.At(now), CompanyClock.Zone);

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
    public void Rejects_submitting_a_week_with_an_open_entry()
    {
        _week.ClockIn(CompanyClock.At("2026-11-01 22:00"), Phone, CompanyClock.Zone);

        var act = () => _week.Submit(_eve, Monday, CompanyClock.Zone);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("OpenEntryInTimesheet");
    }

    [Fact]
    public void Rejects_submitting_a_timesheet_that_is_not_a_draft()
    {
        _week.Submit(_eve, Monday, CompanyClock.Zone);

        var act = () => _week.Submit(_eve, Monday, CompanyClock.Zone);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("TimesheetNotEditable");
    }

    [Fact]
    public void Approves_and_remembers_the_approver_and_the_team_at_that_moment()
    {
        _week.Submit(_eve, Monday, CompanyClock.Zone);

        _week.Approve(_sam, _warehouse, Monday.AddHours(1));

        _week.Status.Should().Be(TimesheetStatus.Approved);
        _week.Approval.Should().Be(new Approval(_sam, Monday.AddHours(1), _warehouse));
        _week.History[^1].Kind.Should().Be(TimesheetEventKind.Approved);
    }

    [Fact]
    public void Approves_and_returns_only_a_submitted_timesheet()
    {
        var approve = () => _week.Approve(_sam, _warehouse, Monday);
        var giveBack = () => _week.Return(_sam, "Tuesday is missing", Monday);

        approve.Should().Throw<DomainException>().Which.Code.Should().Be("TimesheetNotSubmitted");
        giveBack.Should().Throw<DomainException>().Which.Code.Should().Be("TimesheetNotSubmitted");
    }

    [Fact]
    public void Returns_to_draft_with_a_comment_that_clears_on_resubmission()
    {
        _week.Submit(_eve, Monday, CompanyClock.Zone);

        _week.Return(_sam, " Tuesday is missing ", Monday.AddHours(1));

        _week.Status.Should().Be(TimesheetStatus.Draft);
        _week.ReturnComment.Should().Be("Tuesday is missing");
        _week.Submit(_eve, Monday.AddHours(2), CompanyClock.Zone);
        _week.ReturnComment.Should().BeNull();
        _week.History.Select(e => e.Kind).Should().Equal(
            TimesheetEventKind.Submitted, TimesheetEventKind.Returned, TimesheetEventKind.Submitted);
    }

    [Fact]
    public void Reopens_an_approved_timesheet_and_keeps_the_approval_in_the_history()
    {
        Approved();

        _week.Reopen(_ada, "payroll found a missing shift", Monday.AddHours(2));

        _week.Status.Should().Be(TimesheetStatus.Draft);
        _week.Approval.Should().BeNull();
        _week.History.Select(e => e.Kind).Should().EndWith([TimesheetEventKind.Approved, TimesheetEventKind.Reopened]);
        _week.History[^1].Comment.Should().Be("payroll found a missing shift");
    }

    [Fact]
    public void Reopens_directly_only_an_approved_timesheet()
    {
        _week.Submit(_eve, Monday, CompanyClock.Zone);

        var act = () => _week.Reopen(_ada, "reason", Monday);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("TimesheetNotApproved");
    }

    [Fact]
    public void Records_a_correction_request_only_on_a_locked_timesheet()
    {
        var act = () => _week.RecordCorrectionRequested(_eve, "Wednesday shift is missing", Monday);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("TimesheetIsEditable");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reopens_a_submitted_or_approved_timesheet_for_an_accepted_correction(bool approved)
    {
        _week.Submit(_eve, Monday, CompanyClock.Zone);
        if (approved)
        {
            _week.Approve(_sam, _warehouse, Monday);
        }

        _week.RecordCorrectionRequested(_eve, "Wednesday shift is missing", Monday);
        _week.ReopenForCorrection(_sam, "go ahead", Monday.AddHours(1));

        _week.Status.Should().Be(TimesheetStatus.Draft);
        _week.Approval.Should().BeNull();
        _week.History.Select(e => e.Kind).Should().EndWith([TimesheetEventKind.CorrectionRequested, TimesheetEventKind.Reopened]);
    }

    [Fact]
    public void Keeps_new_history_apart_from_what_was_already_saved()
    {
        _week.Submit(_eve, Monday, CompanyClock.Zone);
        var saved = Timesheet.Restore(
            _week.Id, _week.OwnerId, _week.Week, _week.Status, 1, null, null, [], _week.History);

        saved.Return(_sam, "fix it", Monday);

        saved.NewHistory.Should().ContainSingle().Which.Kind.Should().Be(TimesheetEventKind.Returned);
    }

    private void Approved()
    {
        _week.Submit(_eve, Monday, CompanyClock.Zone);
        _week.Approve(_sam, _warehouse, Monday.AddHours(1));
    }
}
