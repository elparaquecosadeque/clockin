using TaskTickets.Api.Common;
using TaskTickets.Api.Features.Auth;
using TaskTickets.Api.Features.Tasks;

namespace TaskTickets.Api.Data;

public sealed class InMemoryStore
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, User> _users = [];
    private readonly Dictionary<string, Guid> _emailIds = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, TaskTicket> _tickets = [];

    public TaskTicket AddTicket(TaskTicket ticket)
    {
        lock (_lock)
        {
            RequireAssignee(ticket.AssignedToUserId);
            _tickets.Add(ticket.Id, ticket);
            return ticket;
        }
    }

    public TaskTicket ReadTicket(Guid id, Guid caller)
    {
        lock (_lock)
        {
            return VisibleTicket(id, caller);
        }
    }

    public TaskTicket[] ListTickets(Guid caller, TicketStatus? status)
    {
        lock (_lock)
        {
            return _tickets.Values.Where(ticket => (ticket.CreatedByUserId == caller || ticket.AssignedToUserId == caller)
                    && (status is null || ticket.Status == status))
                .OrderByDescending(ticket => ticket.CreatedAt).ThenByDescending(ticket => ticket.Id).ToArray();
        }
    }

    public TaskTicket UpdateTicket(Guid id, Guid caller, UpdateTicketRequest request)
    {
        lock (_lock)
        {
            var ticket = VisibleTicket(id, caller);
            var updated = ticket with { Title = request.Title, Description = request.Description, Status = request.Status!.Value, DueDate = request.DueDate };
            _tickets[id] = updated;
            return updated;
        }
    }

    public TaskTicket AssignTicket(Guid id, Guid caller, Guid assignee)
    {
        lock (_lock)
        {
            var ticket = OwnedTicket(id, caller);
            RequireAssignee(assignee);
            var updated = ticket with { AssignedToUserId = assignee };
            _tickets[id] = updated;
            return updated;
        }
    }

    public void DeleteTicket(Guid id, Guid caller)
    {
        lock (_lock)
        {
            OwnedTicket(id, caller);
            _tickets.Remove(id);
        }
    }

    private TaskTicket VisibleTicket(Guid id, Guid caller)
    {
        if (!_tickets.TryGetValue(id, out var ticket) || (ticket.CreatedByUserId != caller && ticket.AssignedToUserId != caller))
        {
            throw new ApiException(404, "Ticket not found.");
        }

        return ticket;
    }

    private TaskTicket OwnedTicket(Guid id, Guid caller)
    {
        var ticket = VisibleTicket(id, caller);
        if (ticket.CreatedByUserId != caller)
        {
            throw new ApiException(403, "Only the creator can reassign or delete this ticket.");
        }

        return ticket;
    }

    private void RequireAssignee(Guid id)
    {
        if (!_users.ContainsKey(id))
        {
            throw new ApiException(400, "The assignee must be an existing user.", "assignedToUserId");
        }
    }

    public User? FindUser(Guid id)
    {
        lock (_lock)
        {
            return _users.GetValueOrDefault(id);
        }
    }

    public User? FindUserByEmail(string normalizedEmail)
    {
        lock (_lock)
        {
            return _emailIds.TryGetValue(normalizedEmail, out var id) ? _users[id] : null;
        }
    }

    public void AddUser(User user)
    {
        lock (_lock)
        {
            if (_emailIds.ContainsKey(user.NormalizedEmail))
            {
                throw new ApiException(409, "A user with this email already exists.");
            }

            _users.Add(user.Id, user);
            _emailIds.Add(user.NormalizedEmail, user.Id);
        }
    }

    public void UpdatePasswordHash(Guid id, string oldHash, string newHash)
    {
        lock (_lock)
        {
            if (_users.TryGetValue(id, out var user) && user.PasswordHash == oldHash)
            {
                _users[id] = user with { PasswordHash = newHash };
            }
        }
    }
}
