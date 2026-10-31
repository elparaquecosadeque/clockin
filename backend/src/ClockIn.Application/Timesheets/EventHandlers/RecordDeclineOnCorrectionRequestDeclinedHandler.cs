using ClockIn.Application.Common.Events;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.EventHandlers;

public sealed class RecordDeclineOnCorrectionRequestDeclinedHandler(ITimesheetRepository timesheetsRepository) : DomainEventSubscriber<CorrectionRequestDeclined>
{
    protected override async Task HandleAsync(CorrectionRequestDeclined domainEvent, CancellationToken ct)
    {
        Timesheet timesheet = await timesheetsRepository.GetAsync(domainEvent.TimesheetId.Value, ct);
        timesheet.RecordCorrectionDeclined(domainEvent.ApproverId, domainEvent.Comment, domainEvent.At);
        await timesheetsRepository.SaveOrUpdateAsync(timesheet, ct);
    }
}
