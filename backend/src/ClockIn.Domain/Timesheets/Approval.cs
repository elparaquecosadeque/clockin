using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;

namespace ClockIn.Domain.Timesheets;

/// <summary>Who approved, when, and the owner's team at that moment (exports keep that team).</summary>
public sealed record Approval(UserId ApproverId, DateTimeOffset At, TeamId? TeamId);
