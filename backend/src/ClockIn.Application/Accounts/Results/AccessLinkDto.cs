namespace ClockIn.Application.Accounts.Results;

public sealed record AccessLinkDto(string Token, DateTimeOffset ExpiresAt);
