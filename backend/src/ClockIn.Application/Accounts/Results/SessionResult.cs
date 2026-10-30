namespace ClockIn.Application.Accounts.Results;

/// <summary>What the API needs to issue an access token and set the refresh cookie.</summary>
public sealed record SessionResult(Guid UserId, Guid SecurityStamp, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
