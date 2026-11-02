using System.Security.Cryptography;
using ClockIn.Application.Accounts.Results;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClockIn.Hosting.Auth;

/// <summary>
/// Signs access tokens that carry identity only: <c>sub</c>, the session (<c>sid</c>), <c>security_stamp</c> and the
/// audience (ADR 0009).
/// </summary>
public sealed class AccessTokenIssuer
{
    public const string Issuer = "ClockIn";
    public const string Audience = "ClockIn.Api";
    public const string SecurityStampClaim = "security_stamp";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly SigningCredentials _credentials;
    private readonly TimeProvider _time;
    private readonly string _audience;

    public AccessTokenIssuer(IOptions<AuthOptions> options, TimeProvider time)
    {
        var key = ECDsa.Create();
        key.ImportFromPem(options.Value.SigningKey);
        Key = new ECDsaSecurityKey(key);
        _credentials = new SigningCredentials(Key, SecurityAlgorithms.EcdsaSha256);
        _time = time;
        _audience = options.Value.Audience;
    }

    public ECDsaSecurityKey Key { get; }

    public AccessTokenResponse Issue(SessionResult session)
    {
        DateTime now = _time.GetUtcNow().UtcDateTime;
        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = _audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + Lifetime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = session.UserId.ToString(),
                [JwtRegisteredClaimNames.Sid] = session.SessionId.ToString(),
                [SecurityStampClaim] = session.SecurityStamp.ToString(),
            },
            SigningCredentials = _credentials,
        });
        return new AccessTokenResponse(token, now + Lifetime);
    }
}
