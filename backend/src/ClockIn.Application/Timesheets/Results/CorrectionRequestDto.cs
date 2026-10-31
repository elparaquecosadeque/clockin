using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets.Results;

public sealed record CorrectionRequestDto(
    Guid Id, CorrectionStatus Status, string Reason, DateTimeOffset RequestedAt, string? DecidedByName, DateTimeOffset? DecidedAt,
    string? DecisionComment);
