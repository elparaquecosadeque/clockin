using System.ComponentModel.DataAnnotations;

namespace TaskTickets.Api.Features.Tasks;

public sealed record UpdateTicketRequest(string Title, [property: StringLength(5000)] string? Description,
    [property: Required] TicketStatus? Status, DateOnly? DueDate)
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = Title?.Trim()!;
}
