using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace ClockIn.Domain.Accounts;

/// <summary>
/// What the client holds: <c>{sessionId}.{secret}</c>. Carrying the session id lets a rotated secret be
/// recognized as reuse without keeping a history of old secrets.
/// </summary>
public sealed record RefreshToken(SessionId SessionId, string Secret, DateTimeOffset ExpiresAt)
{
    public string Value => $"{SessionId.Value:N}.{Secret}";

    public static bool TryParse(string? value, out SessionId sessionId, [NotNullWhen(true)] out string? secret)
    {
        sessionId = default;
        secret = null;
        string[] parts = (value ?? "").Split('.');
        if (parts.Length != 2 || parts[1].Length == 0 || !Guid.TryParseExact(parts[0], "N", out Guid id))
        {
            return false;
        }

        sessionId = new SessionId(id);
        secret = parts[1];
        return true;
    }

    internal static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    internal static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)));
}
