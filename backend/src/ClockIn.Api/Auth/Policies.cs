using ClockIn.Domain.Accounts;

namespace ClockIn.Api.Auth;

/// <summary>One policy per permission, named after it. Registered once in <c>Program.AddAuth</c>.</summary>
public static class Policies
{
    public const string PermissionClaim = "permission";

    public const string PunchTime = Permissions.PunchTime;
    public const string ManageUsers = Permissions.ManageUsers;
    public const string ManageTeams = Permissions.ManageTeams;
    public const string ReviewTeam = Permissions.ReviewTeam;
    public const string ReviewAll = Permissions.ReviewAll;
    public const string ManageTimesheets = Permissions.ManageTimesheets;

    /// <summary>Either review permission: reports and exports, scoped by the handler.</summary>
    public const string Review = "review";

    public static readonly IReadOnlyList<string> All = [PunchTime, ManageUsers, ManageTeams, ReviewTeam, ReviewAll, ManageTimesheets];
}
