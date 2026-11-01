namespace ClockIn.Application.Reporting.Results;

public sealed record ReviewQueueDto(
    IReadOnlyList<QueuedTimesheetDto> Timesheets, IReadOnlyList<QueuedCorrectionDto> Corrections,
    bool HasMoreTimesheets = false, bool HasMoreCorrections = false);
