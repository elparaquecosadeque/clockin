namespace ClockIn.Api.Auth;

public static class RateLimits
{
    /// <summary>Sign-in and refresh, per client IP.</summary>
    public const string SignIn = "sign-in";
}
