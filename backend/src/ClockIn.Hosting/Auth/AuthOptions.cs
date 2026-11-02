using System.ComponentModel.DataAnnotations;

namespace ClockIn.Hosting.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    [Required]
    public string Audience { get; init; } = AccessTokenIssuer.Audience;

    /// <summary>
    /// PEM of the ES256 (P-256) private key that signs access tokens. A secret: user-secrets locally,
    /// the secret store remotely. Development generates a throwaway key when this is empty.
    /// </summary>
    [Required]
    public required string SigningKey { get; init; }

    /// <summary>Sign-in and refresh requests allowed per client per minute.</summary>
    [Range(1, int.MaxValue)]
    public int SignInPermitsPerMinute { get; init; } = 10;
}
