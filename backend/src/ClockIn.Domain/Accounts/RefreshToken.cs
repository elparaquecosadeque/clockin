using System.Diagnostics.CodeAnalysis;
using ClockIn.Domain.Common;

namespace ClockIn.Domain.Accounts;

/// <summary>
/// What the client holds: <c>{sessionId}.{secret}</c>. Carrying the session id lets a rotated secret be
/// recognized as reuse without keeping a history of old secrets.
/// </summary>
public sealed record RefreshToken(SessionId SessionId, string Secret, DateTimeOffset ExpiresAt)
{
    public string Value => OpaqueTokenHelper.Format(SessionId.Value, Secret);

    public static bool TryParse(string? value, out SessionId sessionId, [NotNullWhen(true)] out string? secret)
    {
        bool parsed = OpaqueTokenHelper.TryParse(value, out Guid id, out secret);
        sessionId = new SessionId(id);
        return parsed;
    }
}
