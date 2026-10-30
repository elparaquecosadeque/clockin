using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Results;

public sealed record MeDto(Guid Id, string Name, string Email, Role Role, IReadOnlyList<string> Permissions);
