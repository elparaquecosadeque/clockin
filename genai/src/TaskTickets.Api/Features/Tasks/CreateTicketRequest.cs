using System.ComponentModel.DataAnnotations;

namespace TaskTickets.Api.Features.Tasks;

public sealed record CreateTicketRequest(string Title, [property: StringLength(5000)] string? Description, DateOnly? DueDate, Guid? AssignedToUserId)
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = Title?.Trim()!;
}
