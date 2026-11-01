using ClockIn.Domain.Common;

namespace ClockIn.Domain.Accounts;

/// <summary>One signed-in device. Its refresh token rotates on every use and slides the expiry.</summary>
public sealed class Session
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private Session(SessionId id, UserId userId, Guid securityStamp, string tokenHash, DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        SecurityStamp = securityStamp;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public SessionId Id { get; }

    public UserId UserId { get; }

    /// <summary>The user's stamp at sign-in; a different stamp now means the session must end.</summary>
    public Guid SecurityStamp { get; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>0 until first saved; the repository checks and bumps it on every save.</summary>
    public int Version { get; private init; }

    public static (Session Session, RefreshToken Token) Start(UserId userId, Guid securityStamp, DateTimeOffset now)
    {
        string secret = OpaqueTokenHelper.NewSecret();
        var session = new Session(SessionId.New(), userId, securityStamp, OpaqueTokenHelper.Hash(secret), now + Lifetime);
        return (session, new RefreshToken(session.Id, secret, session.ExpiresAt));
    }

    /// <summary>Rebuilds a saved session. For repositories only; it checks no rules.</summary>
    public static Session Restore(
        SessionId id, UserId userId, Guid securityStamp, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset? revokedAt, int version) =>
        new(id, userId, securityStamp, tokenHash, expiresAt) { RevokedAt = revokedAt, Version = version };

    public bool Matches(string secret) => OpaqueTokenHelper.Matches(TokenHash, secret);

    public (RefreshOutcome Outcome, RefreshToken? Token) Refresh(string secret, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return (RefreshOutcome.Rejected, null);
        }

        if (!Matches(secret))
        {
            return (RefreshOutcome.Reused, null);
        }

        if (now > ExpiresAt)
        {
            return (RefreshOutcome.Rejected, null);
        }

        string next = OpaqueTokenHelper.NewSecret();
        TokenHash = OpaqueTokenHelper.Hash(next);
        ExpiresAt = now + Lifetime;
        return (RefreshOutcome.Refreshed, new RefreshToken(Id, next, ExpiresAt));
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
