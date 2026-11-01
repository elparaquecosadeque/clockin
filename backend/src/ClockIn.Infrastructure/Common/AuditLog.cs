using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace ClockIn.Infrastructure.Common;

/// <summary>Writes security events under the <c>Audit</c> logger category (CODE_STYLE.md, Security hardening).</summary>
public sealed partial class AuditLog(ILoggerFactory loggers) : IAuditLog
{
    private readonly ILogger _logger = loggers.CreateLogger("Audit");

    public void SignedIn(UserId userId) => LogSignedIn(userId.Value);

    public void SignInFailed(string email) => LogSignInFailed(email);

    public void LockedOut(UserId userId) => LogLockedOut(userId.Value);

    public void RefreshTokenReused(UserId userId) => LogRefreshTokenReused(userId.Value);

    public void AccessLinkGenerated(UserId actorId, UserId userId) => LogAccessLinkGenerated(actorId.Value, userId.Value);

    public void RoleChanged(UserId actorId, UserId userId, Role role) => LogRoleChanged(actorId.Value, userId.Value, role);

    public void UserDeactivated(UserId actorId, UserId userId) => LogUserDeactivated(actorId.Value, userId.Value);

    public void UserUnlocked(UserId actorId, UserId userId) => LogUserUnlocked(actorId.Value, userId.Value);

    public void FirstAdminLinkIssued(Email email, string token) => LogFirstAdminLinkIssued(email.Value, token);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in succeeded for user {UserId}")]
    private partial void LogSignedIn(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in failed for {Email}")]
    private partial void LogSignInFailed(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} locked out after repeated failed sign-ins")]
    private partial void LogLockedOut(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh-token reuse for user {UserId}; every session revoked")]
    private partial void LogRefreshTokenReused(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} generated an access link for user {UserId}")]
    private partial void LogAccessLinkGenerated(Guid actorId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} changed the role of user {UserId} to {Role}")]
    private partial void LogRoleChanged(Guid actorId, Guid userId, Role role);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} deactivated user {UserId}")]
    private partial void LogUserDeactivated(Guid actorId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} unlocked user {UserId}")]
    private partial void LogUserUnlocked(Guid actorId, Guid userId);

    // seed-first-admin.feature: the deploy log is the delivery channel, so this entry, alone, carries the token.
    [LoggerMessage(Level = LogLevel.Warning, Message = "First admin {Email} must activate the account at /set-password?token={Token}")]
    private partial void LogFirstAdminLinkIssued(string email, string token);
}
