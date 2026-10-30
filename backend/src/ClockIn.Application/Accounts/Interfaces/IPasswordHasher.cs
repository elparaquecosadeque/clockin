namespace ClockIn.Application.Accounts.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// False when <paramref name="passwordHash"/> is null, but only after the same work as a real check,
    /// so response times don't reveal whether an account exists.
    /// </summary>
    bool Verify(string? passwordHash, string password);
}
