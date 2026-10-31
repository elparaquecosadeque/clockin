using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Results;

/// <summary><see cref="LedTeamId"/> is the active team a supervisor leads, if any.</summary>
public sealed record UserListItemDto(
    Guid Id, string Name, string Email, Role Role, UserStatus Status, Guid? TeamId, string? TeamName, Guid? LedTeamId, bool IsLockedOut);
