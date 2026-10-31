namespace ClockIn.Application.Reporting.Interfaces;

public sealed record TeamRow(Guid Id, string Name, Guid SupervisorId, bool IsArchived);
