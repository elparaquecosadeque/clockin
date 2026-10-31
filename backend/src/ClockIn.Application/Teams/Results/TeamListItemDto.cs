namespace ClockIn.Application.Teams.Results;

/// <summary><see cref="EmployeeCount"/> counts members who aren't deactivated.</summary>
public sealed record TeamListItemDto(Guid Id, string Name, Guid SupervisorId, string SupervisorName, bool IsArchived, int EmployeeCount);
