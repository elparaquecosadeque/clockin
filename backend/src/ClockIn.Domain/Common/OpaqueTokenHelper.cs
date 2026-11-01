using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace ClockIn.Domain.Common;

/// <summary>
/// Tokens handed to clients as <c>{ownerId}.{secret}</c>. Only a SHA-256 of the secret is stored; the owner id
/// lets the server find what the token belongs to without searching by hash.
/// </summary>
public static class OpaqueTokenHelper
{
    public static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public static bool Matches(string? hash, string secret) =>
        hash is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(Hash(secret)));

    public static string Format(Guid ownerId, string secret) => $"{ownerId:N}.{secret}";

    public static bool TryParse(string? value, out Guid ownerId, [NotNullWhen(true)] out string? secret)
    {
        ownerId = Guid.Empty;
        secret = null;
        string[] parts = (value ?? "").Split('.');
        if (parts.Length != 2 || parts[1].Length == 0 || !Guid.TryParseExact(parts[0], "N", out ownerId))
        {
            return false;
        }

        secret = parts[1];
        return true;
    }
}
