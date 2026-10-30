namespace ClockIn.Domain.Accounts;

/// <summary>What each role may do. Code checks these, never roles (glossary: Role).</summary>
public static class Permissions
{
    public const string PunchTime = "time-entries.punch";
    public const string ManageUsers = "users.manage";

    public static IReadOnlyList<string> For(Role role) => role switch
    {
        Role.Employee => [PunchTime],
        Role.Supervisor => [PunchTime],
        Role.Admin => [ManageUsers],
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
