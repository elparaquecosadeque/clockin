using System.Diagnostics.CodeAnalysis;

namespace ClockIn.Domain.Accounts;

/// <summary>An email address, trimmed and lowercased so equality ignores case and surrounding spaces.</summary>
public sealed record Email
{
    public const int MaxLength = 254;

    public Email(string value)
    {
        Value = Normalize(value) ?? throw new ArgumentException($"'{value}' is not a valid email address.", nameof(value));
    }

    public string Value { get; }

    public static bool TryCreate(string? value, [NotNullWhen(true)] out Email? email)
    {
        email = Normalize(value) is null ? null : new Email(value!);
        return email is not null;
    }

    public override string ToString() => Value;

    // Deliberately loose: one '@' with text on both sides. Real validation is the activation link reaching the person.
    private static string? Normalize(string? value)
    {
        string normalized = (value ?? "").Trim().ToLowerInvariant();
        int at = normalized.IndexOf('@', StringComparison.Ordinal);
        bool valid = normalized.Length <= MaxLength
            && at > 0
            && at < normalized.Length - 1
            && at == normalized.LastIndexOf('@')
            && !normalized.Any(char.IsWhiteSpace);
        return valid ? normalized : null;
    }
}
