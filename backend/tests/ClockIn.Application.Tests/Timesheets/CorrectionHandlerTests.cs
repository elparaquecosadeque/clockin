using AwesomeAssertions;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Timesheets.Commands;
using ClockIn.Application.Timesheets.EventHandlers;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using NSubstitute;

namespace ClockIn.Application.Tests.Timesheets;

/// <summary>request, accept and decline correction features, plus the timesheet side through their events.</summary>
public sealed class CorrectionHandlerTests
{
    private static readonly Week LastWeek = TimesheetWorld.WeekOf(2026, 10, 19);

    private readonly TimesheetWorld _world = new();

    [Fact]
    public async Task Requests_a_correction_on_a_locked_timesheet()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Approved);

        await Request(week);

        await _world.CorrectionsRepository.Received(1).SaveOrUpdateAsync(
            Arg.Is<CorrectionRequest>(r => r.TimesheetId == week.Id && r.Status == CorrectionStatus.Pending), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_second_pending_request()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Approved);
        _world.PendingRequest(week);

        var act = () => Request(week);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("CorrectionRequestAlreadyPending");
    }

    [Fact]
    public async Task Forbids_requesting_on_someone_elses_timesheet()
    {
        Timesheet week = _world.Week(_world.Ed, LastWeek, TimesheetStatus.Approved);

        var act = () => Request(week);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Accepts_as_the_current_approver()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Approved);
        CorrectionRequest request = _world.PendingRequest(week);
        _world.SignIn(_world.Sam);

        await Accept(request, "go ahead");

        request.Status.Should().Be(CorrectionStatus.Accepted);
        request.DomainEvents[^1].Should().BeOfType<CorrectionRequestAccepted>();
    }

    [Theory]
    [InlineData("Eve", "Eve")]
    [InlineData("Eve", "Ed")]
    [InlineData("Eve", "Sue")]
    [InlineData("Sam", "Sam")]
    [InlineData("Sam", "Sue")]
    public async Task Forbids_anyone_but_the_current_approver(string owner, string actor)
    {
        CorrectionRequest request = _world.PendingRequest(_world.Week(Person(owner), LastWeek, TimesheetStatus.Approved));
        _world.SignIn(Person(actor));

        var accept = () => Accept(request, null);
        var decline = () => new DeclineCorrectionHandler(_world.CurrentUser, _world.UsersRepository, _world.TeamsRepository, _world.CorrectionsRepository, _world.Clock)
            .HandleAsync(new DeclineCorrectionCommand(request.Id.Value, "no"), CancellationToken.None);

        await accept.Should().ThrowAsync<ForbiddenException>();
        await decline.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Reopens_the_timesheet_when_the_request_is_accepted()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Approved);

        await new ReopenTimesheetOnCorrectionRequestAcceptedHandler(_world.TimesheetsRepository).HandleAsync(
            new CorrectionRequestAccepted(CorrectionRequestId.New(), week.Id, _world.Sam.Id, "go ahead", _world.Clock.GetUtcNow()),
            CancellationToken.None);

        week.Status.Should().Be(TimesheetStatus.Draft);
        week.History[^1].Kind.Should().Be(TimesheetEventKind.Reopened);
        await _world.TimesheetsRepository.Received(1).SaveOrUpdateAsync(week, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Records_requests_and_declines_in_the_timesheet_history()
    {
        Timesheet week = _world.Week(_world.Eve, LastWeek, TimesheetStatus.Approved);
        DateTimeOffset now = _world.Clock.GetUtcNow();

        await new RecordRequestOnCorrectionRequestedHandler(_world.TimesheetsRepository).HandleAsync(
            new CorrectionRequested(CorrectionRequestId.New(), week.Id, _world.Eve.Id, "missing shift", now), CancellationToken.None);
        await new RecordDeclineOnCorrectionRequestDeclinedHandler(_world.TimesheetsRepository).HandleAsync(
            new CorrectionRequestDeclined(CorrectionRequestId.New(), week.Id, _world.Sam.Id, "already paid", now), CancellationToken.None);

        week.History.Select(h => h.Kind).Should().EndWith([TimesheetEventKind.CorrectionRequested, TimesheetEventKind.CorrectionDeclined]);
        week.Status.Should().Be(TimesheetStatus.Approved);
    }

    private User Person(string name) => name switch
    {
        "Eve" => _world.Eve,
        "Ed" => _world.Ed,
        "Sam" => _world.Sam,
        _ => _world.Sue,
    };

    private Task<Guid> Request(Timesheet week) =>
        new RequestCorrectionHandler(_world.CurrentUser, _world.UsersRepository, _world.TeamsRepository, _world.TimesheetsRepository, _world.CorrectionsRepository, _world.Clock)
            .HandleAsync(new RequestCorrectionCommand(week.Id.Value, "Wednesday shift is missing"), CancellationToken.None);

    private Task Accept(CorrectionRequest request, string? comment) =>
        new AcceptCorrectionHandler(_world.CurrentUser, _world.UsersRepository, _world.TeamsRepository, _world.CorrectionsRepository, _world.Clock)
            .HandleAsync(new AcceptCorrectionCommand(request.Id.Value, comment), CancellationToken.None);
}
