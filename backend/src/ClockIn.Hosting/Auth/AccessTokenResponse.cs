namespace ClockIn.Hosting.Auth;

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
