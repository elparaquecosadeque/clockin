using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskTickets.Api.Features.Auth;

namespace TaskTickets.Api.Common;

public sealed class TokenIssuer(SymmetricSecurityKey key, IOptions<AuthOptions> options, TimeProvider clock)
{
    public TokenResponse Issue(User user)
    {
        var now = clock.GetUtcNow();
        var settings = options.Value;
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(settings.LifetimeMinutes).UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", settings.LifetimeMinutes * 60);
    }
}
