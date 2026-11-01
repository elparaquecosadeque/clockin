using AwesomeAssertions;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Timesheets.Commands;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;
using NSubstitute;

namespace ClockIn.Application.Tests.Timesheets;

/// <summary>submit, approve, return and reopen features: who may act, and the pending-correction block.</summary>
public sealed class TimesheetDecisionHandlerTests
{
    private static readonly Week LastWeek = TimesheetWorld.WeekOf(2026, 10, 19);
    private static readonly Week ThisWeek = TimesheetWorld.WeekOf(2026, 10, 26);

    private readonly TimesheetWorld _world = new();

    [Fact]
    public async Task Submits_a_week_with_no_entries()
    {
        _world.Clock.Set("2026-11-02 09:00");

        await Submit(null, ThisWeek);

        await _world.TimesheetsRepository.Received(1).SaveOrUpdateAsync(
            Arg.Is<Timesheet>(t => t.OwnerId == _world.Eve.Id && t.Status == TimesheetStatus.Submitted && t.Entries.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lets_an_admin_submit_on_behalf_of_someone_and_records_the_admin()
    {
        Timesheet week = _world.Week(_world.Ed, LastWeek);
        _world.Ed.Deactivate();
        _world.SignIn(_world.Ada);

        await Submit(_world.Ed.Id, LastWeek);

        week.Status.Should().Be(TimesheetStatus.Submitted);
        week.History[^1].ActorId.Should().Be(_world.Ada.Id);
    }

    [Fact]
    public async Task Forbids_a_supervisor_from_submitting_for_a_team_member()
    {
        _world.SignIn(_world.Sam);

        var act = () => Submit(_world.Eve.Id, LastWeek);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Forbids_admins_from_having_timesheets_of_their_own()
    {
        _world.SignIn(_world.Ada);

        var act = () => Submit(null, LastWeek);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Lets_the_team_supervisor_approve_and_keeps_the_team()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Submitted);
        _world.SignIn(_world.Sam);

        await Approve(week);

        week.Approval!.ApproverId.Should().Be(_world.Sam.Id);
        week.Approval.TeamId.Should().Be(_world.Warehouse.Id);
    }

    [Fact]
    public async Task Lets_an_admin_approve_a_supervisor_timesheet()
    {
        Timesheet week = _world.Week(_world.Sam, LastWeek, TimesheetStatus.Submitted);
        _world.SignIn(_world.Ada);

        await Approve(week);

        week.Status.Should().Be(TimesheetStatus.Approved);
    }

    [Theory]
    [InlineData("Olga", "Sam")]
    [InlineData("Eve", "Ed")]
    [InlineData("Eve", "Eve")]
    [InlineData("Sam", "Sam")]
    [InlineData("Sam", "Sue")]
    public async Task Forbids_anyone_but_the_current_approver(string owner, string actor)
    {
        Timesheet week = _world.Week(Person(owner), LastWeek, TimesheetStatus.Submitted);
        _world.SignIn(Person(actor));

        var act = () => Approve(week);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Follows_the_employee_to_the_new_team()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Submitted);
        _world.Move(_world.Eve, _world.Office);
        _world.SignIn(_world.Sam);

        var bySam = () => Approve(week);

        await bySam.Should().ThrowAsync<ForbiddenException>();
        _world.SignIn(_world.Sue);
        await Approve(week);
        week.Status.Should().Be(TimesheetStatus.Approved);
    }

    [Fact]
    public async Task Blocks_approve_return_and_reopen_while_a_correction_request_is_pending()
    {
        Timesheet submitted = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Submitted);
        Timesheet approved = _world.Week(_world.Ed, LastWeek, TimesheetStatus.Approved);
        _world.PendingRequest(submitted);
        _world.PendingRequest(approved);
        _world.SignIn(_world.Ada);

        var approve = () => Approve(submitted);
        var giveBack = () => Return(submitted, "fix");
        var reopen = () => Reopen(approved, "payroll");

        (await approve.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("PendingCorrectionRequest");
        (await giveBack.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("PendingCorrectionRequest");
        (await reopen.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("PendingCorrectionRequest");
    }

    [Fact]
    public async Task Returns_with_a_comment_the_owner_sees()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Submitted);
        _world.SignIn(_world.Sam);

        await Return(week, "Tuesday is missing");

        week.Status.Should().Be(TimesheetStatus.Draft);
        week.ReturnComment.Should().Be("Tuesday is missing");
    }

    [Theory]
    [InlineData("Sam")]
    [InlineData("Eve")]
    public async Task Lets_only_admins_reopen(string actor)
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Approved);
        _world.SignIn(Person(actor));

        var act = () => Reopen(week, "payroll found a missing shift");

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Reopens_an_approved_timesheet_for_an_admin()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Approved);
        _world.SignIn(_world.Ada);

        await Reopen(week, "payroll found a missing shift");

        week.Status.Should().Be(TimesheetStatus.Draft);
    }

    [Fact]
    public async Task Reports_an_unknown_timesheet_as_not_found()
    {
        _world.SignIn(_world.Ada);

        var act = () => new ApproveTimesheetHandler(_world.CurrentUser, _world.Access, _world.TimesheetsRepository, _world.CorrectionsRepository, _world.Clock)
            .HandleAsync(new ApproveTimesheetCommand(Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private User Person(string name) => name switch
    {
        "Eve" => _world.Eve,
        "Ed" => _world.Ed,
        "Olga" => _world.Olga,
        "Sam" => _world.Sam,
        "Sue" => _world.Sue,
        _ => _world.Ada,
    };

    private Task Submit(UserId? owner, Week week) =>
        new SubmitTimesheetHandler(_world.CurrentUser, _world.Access, _world.TimesheetsRepository, _world.Clock)
            .HandleAsync(new SubmitTimesheetCommand(owner?.Value, week.Start), CancellationToken.None);

    private Task Approve(Timesheet week) =>
        new ApproveTimesheetHandler(_world.CurrentUser, _world.Access, _world.TimesheetsRepository, _world.CorrectionsRepository, _world.Clock)
            .HandleAsync(new ApproveTimesheetCommand(week.Id.Value), CancellationToken.None);

    private Task Return(Timesheet week, string comment) =>
        new ReturnTimesheetHandler(_world.CurrentUser, _world.Access, _world.TimesheetsRepository, _world.CorrectionsRepository, _world.Clock)
            .HandleAsync(new ReturnTimesheetCommand(week.Id.Value, comment), CancellationToken.None);

    private Task Reopen(Timesheet week, string reason) =>
        new ReopenTimesheetHandler(_world.CurrentUser, _world.Access, _world.TimesheetsRepository, _world.CorrectionsRepository, _world.Clock)
            .HandleAsync(new ReopenTimesheetCommand(week.Id.Value, reason), CancellationToken.None);
}
