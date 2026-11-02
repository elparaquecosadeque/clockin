namespace TaskTickets.Api.Features.Tasks;

public sealed record TicketPage(IReadOnlyList<TicketResponse> Items, int Page, int PageSize, int TotalCount);
