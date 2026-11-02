using System.Security.Claims;

namespace TaskTickets.Api.Features.Tasks;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/task-tickets").WithTags("Task tickets")
            .ProducesProblem(401).ProducesValidationProblem();
        group.MapPost("", (CreateTicketRequest request, ClaimsPrincipal caller, TaskService service) =>
        {
            var ticket = service.Create(CallerId(caller), request);
            return TypedResults.Created($"/api/task-tickets/{ticket.Id}", ticket);
        }).WithName("CreateTicket").WithSummary("Create a todo ticket; default assignee is the creator.");
        group.MapGet("", (ClaimsPrincipal caller, TaskService service, string? status, int? page, int? pageSize) =>
            TypedResults.Ok(service.List(CallerId(caller), status, page ?? 1, pageSize ?? 20)))
            .WithName("ListTickets").WithSummary("List tickets created by or assigned to you, newest first.");
        group.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal caller, TaskService service) => TypedResults.Ok(service.Read(id, CallerId(caller))))
            .WithName("ReadTicket").WithSummary("Read a ticket visible to you.").ProducesProblem(404);
        group.MapPut("/{id:guid}", (Guid id, UpdateTicketRequest request, ClaimsPrincipal caller, TaskService service) =>
            TypedResults.Ok(service.Update(id, CallerId(caller), request)))
            .WithName("UpdateTicket").WithSummary("Replace title, description, status, and due date.").ProducesProblem(404);
        group.MapPut("/{id:guid}/assignee", (Guid id, AssignTicketRequest request, ClaimsPrincipal caller, TaskService service) =>
            TypedResults.Ok(service.Assign(id, CallerId(caller), request.AssignedToUserId!.Value)))
            .WithName("AssignTicket").WithSummary("Reassign a ticket you created.").ProducesProblem(403).ProducesProblem(404);
        group.MapDelete("/{id:guid}", (Guid id, ClaimsPrincipal caller, TaskService service) =>
        {
            service.Delete(id, CallerId(caller));
            return TypedResults.NoContent();
        }).WithName("DeleteTicket").WithSummary("Permanently delete a ticket you created.").ProducesProblem(403).ProducesProblem(404);
    }

    private static Guid CallerId(ClaimsPrincipal caller) => Guid.Parse(caller.FindFirstValue("sub")!);
}
