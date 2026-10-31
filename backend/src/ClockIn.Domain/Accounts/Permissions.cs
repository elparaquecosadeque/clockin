namespace ClockIn.Domain.Accounts;

/// <summary>What each role may do. Code checks these, never roles (glossary: Role).</summary>
public static class Permissions
{
    /// <summary>Punch, and own timesheets: edit entries, submit, request corrections.</summary>
    public const string PunchTime = "time-entries.punch";
    public const string ManageUsers = "users.manage";
    public const string ManageTeams = "teams.manage";

    /// <summary>Review, report on and export the timesheets of the team one leads.</summary>
    public const string ReviewTeam = "timesheets.review-team";

    /// <summary>Review, report on and export everyone's timesheets, supervisors included.</summary>
    public const string ReviewAll = "timesheets.review-all";

    /// <summary>Change anyone's Draft with a reason, submit on their behalf, and reopen approved weeks.</summary>
    public const string ManageTimesheets = "timesheets.manage";

    public static IReadOnlyList<string> For(Role role) => role switch
    {
        Role.Employee => [PunchTime],
        Role.Supervisor => [PunchTime, ReviewTeam],
        Role.Admin => [ManageUsers, ManageTeams, ReviewAll, ManageTimesheets],
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static bool Can(this Role role, string permission) => For(role).Contains(permission);
}
