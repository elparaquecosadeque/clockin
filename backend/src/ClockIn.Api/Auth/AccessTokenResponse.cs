namespace ClockIn.Api.Auth;

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
