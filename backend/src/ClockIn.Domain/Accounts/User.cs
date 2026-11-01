using Ardalis.GuardClauses;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;

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

    public Role Role { get; private set; }

    public UserStatus Status { get; private set; } = UserStatus.PendingActivation;

    /// <summary>Null until the user sets a password with an access link.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Changes whenever earlier sessions must stop working (password set, deactivation, role change).</summary>
    public Guid SecurityStamp { get; private set; } = Guid.CreateVersion7();

    public int FailedSignIns { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>Hash of the current access link's secret; null when there is none. A new link replaces the old one.</summary>
    public string? AccessLinkHash { get; private set; }

    public DateTimeOffset? AccessLinkExpiresAt { get; private set; }

    /// <summary>The team an employee belongs to. Null for supervisors and admins, who belong to no team.</summary>
    public TeamId? TeamId { get; private set; }

    /// <summary>One of <see cref="PreferencesConstants.Languages"/>.</summary>
    public string Language { get; private set; } = PreferencesConstants.English;

    /// <summary>One of <see cref="PreferencesConstants.TimeFormats"/>.</summary>
    public string TimeFormat { get; private set; } = PreferencesConstants.TwentyFourHour;

    /// <summary>0 until first saved; the repository checks and bumps it on every save.</summary>
    public int Version { get; private init; }

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil > now;

    public static User Create(
        Email email, string name, Role role, string language = PreferencesConstants.English, string timeFormat = PreferencesConstants.TwentyFourHour)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.StringTooLong(name.Trim(), MaxNameLength);
        var user = new User(UserId.New(), email, name.Trim(), role);
        user.UpdatePreferences(language, timeFormat);
        return user;
    }

    /// <summary>Rebuilds a saved user. For repositories only; it checks no rules.</summary>
    public static User Restore(
        UserId id, Email email, string name, Role role, UserStatus status, string? passwordHash,
        Guid securityStamp, int failedSignIns, DateTimeOffset? lockedUntil,
        string? accessLinkHash, DateTimeOffset? accessLinkExpiresAt, TeamId? teamId, string language, string timeFormat, int version) =>
        new(id, email, name, role)
        {
            TeamId = teamId,
            Language = language,
            TimeFormat = timeFormat,
            Status = status,
            PasswordHash = passwordHash,
            SecurityStamp = securityStamp,
            FailedSignIns = failedSignIns,
            LockedUntil = lockedUntil,
            AccessLinkHash = accessLinkHash,
            AccessLinkExpiresAt = accessLinkExpiresAt,
            Version = version,
        };

    /// <summary>Activates a pending user or resets an active one's password, ending every earlier session.</summary>
    public void SetPassword(string passwordHash)
    {
        EnsureNotDeactivated();
        PasswordHash = Guard.Against.NullOrWhiteSpace(passwordHash);
        Status = UserStatus.Active;
        FailedSignIns = 0;
        LockedUntil = null;
        AccessLinkHash = null;
        AccessLinkExpiresAt = null;
        SecurityStamp = Guid.CreateVersion7();
    }

    /// <summary>A new one-time link, replacing any earlier one. The current password and sessions keep working.</summary>
    public AccessLink IssueAccessLink(DateTimeOffset now)
    {
        EnsureNotDeactivated();
        string secret = OpaqueTokenHelper.NewSecret();
        AccessLinkHash = OpaqueTokenHelper.Hash(secret);
        AccessLinkExpiresAt = now + AccessLink.Validity;
        return new AccessLink(Id, secret, AccessLinkExpiresAt.Value);
    }

    /// <remarks>Every failure gives the same answer, so a link reveals nothing about the account.</remarks>
    public void SetPasswordWithLink(string secret, string passwordHash, DateTimeOffset now)
    {
        if (Status == UserStatus.Deactivated || now > AccessLinkExpiresAt || !OpaqueTokenHelper.Matches(AccessLinkHash, secret))
        {
            throw new DomainException("InvalidOrExpiredLink", "The link is invalid or has expired. Ask an admin for a new one.");
        }

        SetPassword(passwordHash);
    }

    /// <summary>Blocks access at once and voids any access link. The handler checks open entries and led teams.</summary>
    public void Deactivate()
    {
        EnsureNotDeactivated();
        Status = UserStatus.Deactivated;
        AccessLinkHash = null;
        AccessLinkExpiresAt = null;
        SecurityStamp = Guid.CreateVersion7();
    }

    /// <summary>Back to PendingActivation with a fresh link; the old password no longer works.</summary>
    /// <remarks>An employee needs an active team: the chosen one, or else the one they had.</remarks>
    public AccessLink Reactivate(Team? team, DateTimeOffset now)
    {
        if (Status != UserStatus.Deactivated)
        {
            throw new DomainException("UserIsNotDeactivated", "The user is not deactivated.");
        }

        if (Role == Role.Employee)
        {
            if (team is null || team.IsArchived)
            {
                throw new DomainException("TeamIsArchived", "The team is archived. Choose an active team.");
            }

            TeamId = team.Id;
        }

        Status = UserStatus.PendingActivation;
        PasswordHash = null;
        FailedSignIns = 0;
        LockedUntil = null;
        SecurityStamp = Guid.CreateVersion7();
        return IssueAccessLink(now);
    }

    /// <summary>Swaps Employee and Supervisor. Admin is never granted or removed this way.</summary>
    /// <remarks>The handler checks that a supervisor leads no team before demoting.</remarks>
    public void ChangeRole(Role role, Team? team)
    {
        if (Role == Role.Admin || role == Role.Admin)
        {
            throw new DomainException("AdminRoleIsFixed", "Admin is never granted or removed by a role change.");
        }

        EnsureNotDeactivated();
        Role = role;
        TeamId = null;
        if (role == Role.Employee)
        {
            MoveTo(Guard.Against.Null(team));
        }

        SecurityStamp = Guid.CreateVersion7();
    }

    public void Unlock()
    {
        FailedSignIns = 0;
        LockedUntil = null;
    }

    /// <summary>
    /// A new password ends every other session; the caller starts a fresh one for this device. A wrong current
    /// password counts like a failed sign-in, so a stolen session can't be used to guess it.
    /// </summary>
    public PasswordChangeOutcome ChangePassword(bool currentPasswordMatches, string newPasswordHash, DateTimeOffset now)
    {
        if (LockedUntil > now)
        {
            return PasswordChangeOutcome.Rejected;
        }

        if (!currentPasswordMatches)
        {
            return CountFailure(now) ? PasswordChangeOutcome.LockedOut : PasswordChangeOutcome.Rejected;
        }

        SetPassword(newPasswordHash);
        return PasswordChangeOutcome.Changed;
    }

    /// <summary>Keeps every session valid.</summary>
    public void UpdatePreferences(string language, string timeFormat)
    {
        Guard.Against.InvalidInput(language, nameof(language), PreferencesConstants.Languages.Contains);
        Guard.Against.InvalidInput(timeFormat, nameof(timeFormat), PreferencesConstants.TimeFormats.Contains);
        Language = language;
        TimeFormat = timeFormat;
    }

    /// <summary>Puts an employee in a team. Pending work follows, since the approver is derived from the team.</summary>
    public void MoveTo(Team team)
    {
        if (Role != Role.Employee)
        {
            throw new DomainException("UserIsNotEmployee", "Only employees belong to a team.");
        }

        EnsureNotDeactivated();
        if (team.IsArchived)
        {
            throw new DomainException("TeamIsArchived", "The team is archived.");
        }

        if (TeamId == team.Id)
        {
            throw new DomainException("AlreadyInTeam", "The employee is already in this team.");
        }

        TeamId = team.Id;
    }

    private void EnsureNotDeactivated()
    {
        if (Status == UserStatus.Deactivated)
        {
            throw new DomainException("UserIsDeactivated", "The user is deactivated.");
        }
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

        return CountFailure(now) ? SignInOutcome.LockedOut : SignInOutcome.Rejected;
    }

    /// <summary>Counts a wrong password; the fifth in a row locks the account. True when this one locked it.</summary>
    private bool CountFailure(DateTimeOffset now)
    {
        FailedSignIns++;
        if (FailedSignIns < MaxFailedSignIns)
        {
            return false;
        }

        FailedSignIns = 0;
        LockedUntil = now + LockoutLength;
        return true;
    }
}
