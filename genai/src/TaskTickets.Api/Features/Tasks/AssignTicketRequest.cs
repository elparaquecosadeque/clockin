using System.ComponentModel.DataAnnotations;

namespace TaskTickets.Api.Features.Tasks;

public sealed record AssignTicketRequest([property: Required] Guid? AssignedToUserId);
