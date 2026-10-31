using ClockIn.Application.Common.Events;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.EventHandlers;

public sealed class ReopenTimesheetOnCorrectionRequestAcceptedHandler(ITimesheetRepository timesheets) : DomainEventSubscriber<CorrectionRequestAccepted>
{
    protected override async Task HandleAsync(CorrectionRequestAccepted domainEvent, CancellationToken ct)
    {
        Timesheet timesheet = await timesheets.GetAsync(domainEvent.TimesheetId.Value, ct);
        timesheet.ReopenForCorrection(domainEvent.ApproverId, domainEvent.Comment, domainEvent.At);
        await timesheets.SaveAsync(timesheet, ct);
    }
}
