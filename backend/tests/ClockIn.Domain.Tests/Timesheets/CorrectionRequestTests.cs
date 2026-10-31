using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;

namespace ClockIn.Domain.Tests.Timesheets;

/// <summary>accept-correction.feature and decline-correction.feature: the request's own state.</summary>
public sealed class CorrectionRequestTests
{
    private static readonly DateTimeOffset Now = CompanyClock.At("2026-10-30 10:00");

    private readonly UserId _sam = UserId.New();
    private readonly CorrectionRequest _request =
        CorrectionRequest.Create(TimesheetId.New(), UserId.New(), " Wednesday shift is missing ", Now);

    [Fact]
    public void Starts_pending_with_a_trimmed_reason()
    {
        _request.Status.Should().Be(CorrectionStatus.Pending);
        _request.Reason.Should().Be("Wednesday shift is missing");
    }

    [Fact]
    public void Accepts_with_an_optional_comment()
    {
        _request.Accept(_sam, null, Now);

        _request.Status.Should().Be(CorrectionStatus.Accepted);
        _request.DecidedBy.Should().Be(_sam);
        _request.DecidedAt.Should().Be(Now);
        _request.DecisionComment.Should().BeNull();
    }

    [Fact]
    public void Declines_with_a_comment()
    {
        _request.Decline(_sam, "those hours were already paid", Now);

        _request.Status.Should().Be(CorrectionStatus.Declined);
        _request.DecisionComment.Should().Be("those hours were already paid");
    }

    [Fact]
    public void Raises_an_event_for_each_step_so_the_timesheet_follows_in_the_same_transaction()
    {
        _request.Accept(_sam, " go ahead ", Now);

        _request.DomainEvents.Should().HaveCount(2);
        _request.DomainEvents[0].Should().BeOfType<CorrectionRequested>();
        _request.DomainEvents[1].Should().Be(new CorrectionRequestAccepted(_request.Id, _request.TimesheetId, _sam, "go ahead", Now));
        _request.ClearDomainEvents();
        _request.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Decides_only_once()
    {
        _request.Accept(_sam, null, Now);

        var accept = () => _request.Accept(_sam, null, Now);
        var decline = () => _request.Decline(_sam, "no", Now);

        accept.Should().Throw<DomainException>().Which.Code.Should().Be("CorrectionRequestNotPending");
        decline.Should().Throw<DomainException>().Which.Code.Should().Be("CorrectionRequestNotPending");
    }
}
