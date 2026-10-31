using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Demo.Results;

/// <summary>What a demo account is good for trying. The frontend words each scenario in the user's language.</summary>
public enum DemoScenario
{
    NotStarted,
    ClockedIn,
    ClockedOut,
    ForgotToClockOut,
    LockedOut,
    PendingActivation,
    Deactivated,
    Admin,
}

/// <summary>A demo account. <c>Team</c> is the employee's team, or the team a supervisor leads.</summary>
public sealed record DemoAccount(string Email, string Name, Role Role, string? Team, DemoScenario Scenario);

public sealed record DemoAccountsDto(string Password, IReadOnlyList<DemoAccount> Accounts);
