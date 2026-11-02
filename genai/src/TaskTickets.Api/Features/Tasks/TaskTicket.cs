namespace TaskTickets.Api.Features.Tasks;

public sealed record TaskTicket(Guid Id, string Title, string? Description, TicketStatus Status, DateOnly? DueDate,
    Guid CreatedByUserId, Guid AssignedToUserId, DateTimeOffset CreatedAt);
