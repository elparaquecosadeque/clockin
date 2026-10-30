using ClockIn.Domain.Accounts;

namespace ClockIn.Api.Auth;

/// <summary>One policy per permission, named after it. Registered once in <c>Program.AddAuth</c>.</summary>
public static class Policies
{
    public const string PermissionClaim = "permission";

    public const string PunchTime = Permissions.PunchTime;

    public static readonly IReadOnlyList<string> All = [PunchTime];
}
