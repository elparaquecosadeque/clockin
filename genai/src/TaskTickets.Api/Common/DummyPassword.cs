using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using TaskTickets.Api.Features.Auth;

namespace TaskTickets.Api.Common;

public sealed class DummyPassword
{
    public User User { get; }

    public DummyPassword(PasswordHasher<User> hasher)
    {
        var user = new User(Guid.Empty, "", "", "", "");
        User = user with { PasswordHash = hasher.HashPassword(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))) };
    }
}
