using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using Microsoft.AspNetCore.Identity;

namespace ClockIn.Infrastructure.Accounts;

/// <summary>ASP.NET Core Identity's versioned hasher (PBKDF2), without the Identity stores.</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<User> Hasher = new();

    // Verified when there is no real hash, so a missing account costs the same time as a wrong password.
    private static readonly string DummyHash = Hasher.HashPassword(null!, "no account has this password");

    public string Hash(string password) => Hasher.HashPassword(null!, password);

    public bool Verify(string? passwordHash, string password)
    {
        PasswordVerificationResult result = Hasher.VerifyHashedPassword(null!, passwordHash ?? DummyHash, password);
        return passwordHash is not null && result != PasswordVerificationResult.Failed;
    }
}
