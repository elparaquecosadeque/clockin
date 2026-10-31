using ClockIn.Application.Common.Events;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.EventHandlers;

public sealed class RecordRequestOnCorrectionRequestedHandler(ITimesheetRepository timesheets) : DomainEventSubscriber<CorrectionRequested>
{
    protected override async Task HandleAsync(CorrectionRequested domainEvent, CancellationToken ct)
    {
        Timesheet timesheet = await timesheets.GetAsync(domainEvent.TimesheetId.Value, ct);
        timesheet.RecordCorrectionRequested(domainEvent.OwnerId, domainEvent.Reason, domainEvent.At);
        await timesheets.SaveAsync(timesheet, ct);
    }
}
