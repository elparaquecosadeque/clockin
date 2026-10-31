namespace ClockIn.Application.Teams.Results;

public sealed record TeamDto(Guid Id, string Name, Guid SupervisorId, bool IsArchived);
