using System.Diagnostics.CodeAnalysis;
using ClockIn.Domain.Common;

namespace ClockIn.Domain.Accounts;

/// <summary>A one-time link to activate an account or reset its password (glossary: Access link).</summary>
public sealed record AccessLink(UserId UserId, string Secret, DateTimeOffset ExpiresAt)
{
    public static readonly TimeSpan Validity = TimeSpan.FromHours(72);

    /// <summary>The token the person receives, as <c>{userId}.{secret}</c>.</summary>
    public string Token => OpaqueToken.Format(UserId.Value, Secret);

    public static bool TryParse(string? token, out UserId userId, [NotNullWhen(true)] out string? secret)
    {
        bool parsed = OpaqueToken.TryParse(token, out Guid id, out secret);
        userId = new UserId(id);
        return parsed;
    }
}
