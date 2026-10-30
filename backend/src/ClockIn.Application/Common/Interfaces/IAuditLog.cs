using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Common.Interfaces;

/// <summary>Security events (CODE_STYLE.md, Security hardening). Never pass passwords or tokens.</summary>
public interface IAuditLog
{
    void SignedIn(UserId userId);

    void SignInFailed(string email);

    void LockedOut(UserId userId);

    void RefreshTokenReused(UserId userId);
}
