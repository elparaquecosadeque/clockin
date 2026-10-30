using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Common.Interfaces;

/// <summary>Security events (CODE_STYLE.md, Security hardening). Never pass passwords or tokens.</summary>
public interface IAuditLog
{
    void SignedIn(UserId userId);

    void SignInFailed(string email);

    void LockedOut(UserId userId);

    void RefreshTokenReused(UserId userId);

    void AccessLinkGenerated(UserId actorId, UserId userId);

    /// <summary>The one event that carries a token: the deploy log is how the first admin receives it.</summary>
    void FirstAdminLinkIssued(Email email, string token);
}
