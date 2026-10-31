using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;

namespace ClockIn.Domain.Timesheets;

/// <summary>An owner's ask to reopen a locked timesheet. At most one is pending per timesheet (database constraint).</summary>
public sealed class CorrectionRequest : IRaisesDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    private CorrectionRequest(CorrectionRequestId id, TimesheetId timesheetId, UserId ownerId, string reason, DateTimeOffset requestedAt)
    {
        Id = id;
        TimesheetId = timesheetId;
        OwnerId = ownerId;
        Reason = reason;
        RequestedAt = requestedAt;
    }

    public CorrectionRequestId Id { get; }

    public TimesheetId TimesheetId { get; }

    public UserId OwnerId { get; }

    public string Reason { get; }

    public DateTimeOffset RequestedAt { get; }

    public CorrectionStatus Status { get; private set; }

    public UserId? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? DecisionComment { get; private set; }

    /// <summary>0 until first saved; the repository checks and bumps it on every save.</summary>
    public int Version { get; private init; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    public static CorrectionRequest Create(TimesheetId timesheetId, UserId ownerId, string reason, DateTimeOffset now)
    {
        var request = new CorrectionRequest(CorrectionRequestId.New(), timesheetId, ownerId, reason.Trim(), now);
        request._events.Add(new CorrectionRequested(request.Id, timesheetId, ownerId, request.Reason, now));
        return request;
    }

    /// <summary>Rebuilds a saved request. For repositories only; it checks no rules.</summary>
    public static CorrectionRequest Restore(
        CorrectionRequestId id, TimesheetId timesheetId, UserId ownerId, string reason, DateTimeOffset requestedAt,
        CorrectionStatus status, UserId? decidedBy, DateTimeOffset? decidedAt, string? decisionComment, int version) =>
        new(id, timesheetId, ownerId, reason, requestedAt)
        {
            Status = status,
            DecidedBy = decidedBy,
            DecidedAt = decidedAt,
            DecisionComment = decisionComment,
            Version = version,
        };

    /// <remarks>The timesheet reopens in the same transaction, through <see cref="CorrectionRequestAccepted"/>.</remarks>
    public void Accept(UserId approver, string? comment, DateTimeOffset now)
    {
        Decide(CorrectionStatus.Accepted, approver, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), now);
        _events.Add(new CorrectionRequestAccepted(Id, TimesheetId, approver, DecisionComment, now));
    }

    public void Decline(UserId approver, string comment, DateTimeOffset now)
    {
        Decide(CorrectionStatus.Declined, approver, comment.Trim(), now);
        _events.Add(new CorrectionRequestDeclined(Id, TimesheetId, approver, DecisionComment!, now));
    }

    public void ClearDomainEvents() => _events.Clear();

    private void Decide(CorrectionStatus status, UserId approver, string? comment, DateTimeOffset now)
    {
        if (Status != CorrectionStatus.Pending)
        {
            throw new DomainException("CorrectionRequestNotPending", "The request was already decided.");
        }

        Status = status;
        DecidedBy = approver;
        DecidedAt = now;
        DecisionComment = comment;
    }
}
