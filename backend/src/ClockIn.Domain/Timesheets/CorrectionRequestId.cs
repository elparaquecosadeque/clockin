namespace ClockIn.Domain.Timesheets;

public readonly record struct CorrectionRequestId(Guid Value)
{
    public static CorrectionRequestId New() => new(Guid.CreateVersion7());
}
