using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Reporting.Interfaces;

public sealed record PersonRow(Guid Id, string Name, string Email, Role Role, UserStatus Status, Guid? TeamId, DateTimeOffset? LockedUntil = null);
