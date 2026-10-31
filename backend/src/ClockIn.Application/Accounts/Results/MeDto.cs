using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Accounts.Results;

/// <summary>The signed-in user. Team and supervisor are set for employees only.</summary>
public sealed record MeDto(
    Guid Id,
    string Name,
    string Email,
    Role Role,
    IReadOnlyList<string> Permissions,
    string Language,
    string TimeFormat,
    string? TeamName,
    string? SupervisorName);
