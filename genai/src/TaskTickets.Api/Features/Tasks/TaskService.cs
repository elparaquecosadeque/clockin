using TaskTickets.Api.Common;
using TaskTickets.Api.Data;

namespace TaskTickets.Api.Features.Tasks;

public sealed class TaskService(InMemoryStore store, TimeProvider clock)
{
    public TicketResponse Create(Guid caller, CreateTicketRequest request) => Map(store.AddTicket(new TaskTicket(
        Guid.CreateVersion7(), request.Title, request.Description, TicketStatus.Todo, request.DueDate,
        caller, request.AssignedToUserId ?? caller, clock.GetUtcNow())));

    public TicketResponse Read(Guid id, Guid caller) => Map(store.ReadTicket(id, caller));

    public TicketResponse Update(Guid id, Guid caller, UpdateTicketRequest request) => Map(store.UpdateTicket(id, caller, request));

    public TicketResponse Assign(Guid id, Guid caller, Guid assignee) => Map(store.AssignTicket(id, caller, assignee));

    public void Delete(Guid id, Guid caller) => store.DeleteTicket(id, caller);

    public TicketPage List(Guid caller, string? status, int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ApiException(400, "Page must be at least 1.", "page");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ApiException(400, "Page size must be between 1 and 100.", "pageSize");
        }

        TicketStatus? filter = status switch
        {
            null => null,
            "todo" => TicketStatus.Todo,
            "in_progress" => TicketStatus.InProgress,
            "done" => TicketStatus.Done,
            _ => throw new ApiException(400, "Status must be todo, in_progress, or done.", "status")
        };
        var tickets = store.ListTickets(caller, filter);
        var skip = (long)(page - 1) * pageSize;
        var items = skip >= tickets.Length ? [] : tickets.Skip((int)skip).Take(pageSize).Select(Map).ToArray();
        return new TicketPage(items, page, pageSize, tickets.Length);
    }

    private static TicketResponse Map(TaskTicket ticket) => new(ticket.Id, ticket.Title, ticket.Description,
        ticket.Status, ticket.DueDate, ticket.CreatedByUserId, ticket.AssignedToUserId);
}
