namespace ClockIn.Application.Reporting.Results;

/// <summary><see cref="OverLimit"/> flags an entry open for more than the 16-hour maximum.</summary>
public sealed record ClockedInDto(Guid UserId, string Name, string? TeamName, DateTimeOffset Since, long Seconds, bool OverLimit);
