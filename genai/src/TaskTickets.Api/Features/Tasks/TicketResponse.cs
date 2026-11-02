namespace TaskTickets.Api.Features.Tasks;

public sealed record TicketResponse(Guid Id, string Title, string? Description, TicketStatus Status, DateOnly? DueDate,
    Guid CreatedByUserId, Guid AssignedToUserId);
