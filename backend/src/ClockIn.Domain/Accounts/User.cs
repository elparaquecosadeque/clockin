using Ardalis.GuardClauses;
using ClockIn.Domain.Common;

namespace ClockIn.Domain.Accounts;

/// <summary>Anyone with an account. Never deleted.</summary>
public sealed class User
{
    public const int MaxNameLength = 100;
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan LockoutLength = TimeSpan.FromMinutes(15);

    private User(UserId id, Email email, string name, Role role)
    {
        Id = id;
        Email = email;
        Name = name;
        Role = role;
    }

    public UserId Id { get; }

    public Email Email { get; }

    public string Name { get; }

    public Role Role { get; }

    public UserStatus Status { get; private set; } = UserStatus.PendingActivation;

    /// <summary>Null until the user sets a password with an access link.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Changes whenever earlier sessions must stop working (password set, deactivation, role change).</summary>
    public Guid SecurityStamp { get; private set; } = Guid.CreateVersion7();

    public int FailedSignIns { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>0 until first saved; the repository checks and bumps it on every save.</summary>
    public int Version { get; private init; }

    public static User Create(Email email, string name, Role role)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.StringTooLong(name.Trim(), MaxNameLength);
        return new User(UserId.New(), email, name.Trim(), role);
    }

    /// <summary>Rebuilds a saved user. For repositories only; it checks no rules.</summary>
    public static User Restore(
        UserId id, Email email, string name, Role role, UserStatus status, string? passwordHash,
        Guid securityStamp, int failedSignIns, DateTimeOffset? lockedUntil, int version) =>
        new(id, email, name, role)
        {
            Status = status,
            PasswordHash = passwordHash,
            SecurityStamp = securityStamp,
            FailedSignIns = failedSignIns,
            LockedUntil = lockedUntil,
            Version = version,
        };

    /// <summary>Activates a pending user or resets an active one's password, ending every earlier session.</summary>
    public void SetPassword(string passwordHash)
    {
        if (Status == UserStatus.Deactivated)
        {
            throw new DomainException("UserIsDeactivated", "The user is deactivated.");
        }

        PasswordHash = Guard.Against.NullOrWhiteSpace(passwordHash);
        Status = UserStatus.Active;
        FailedSignIns = 0;
        LockedUntil = null;
        SecurityStamp = Guid.CreateVersion7();
    }

    public void Deactivate()
    {
        Status = UserStatus.Deactivated;
        SecurityStamp = Guid.CreateVersion7();
    }

    /// <summary>Applies the lockout rules to a sign-in attempt whose password was already checked against <see cref="PasswordHash"/>.</summary>
    public SignInOutcome SignIn(bool passwordMatches, DateTimeOffset now)
    {
        if (Status != UserStatus.Active || LockedUntil > now)
        {
            return SignInOutcome.Rejected;
        }

        if (passwordMatches)
        {
            FailedSignIns = 0;
            LockedUntil = null;
            return SignInOutcome.SignedIn;
        }

        FailedSignIns++;
        if (FailedSignIns < MaxFailedSignIns)
        {
            return SignInOutcome.Rejected;
        }

        FailedSignIns = 0;
        LockedUntil = now + LockoutLength;
        return SignInOutcome.LockedOut;
    }
}
