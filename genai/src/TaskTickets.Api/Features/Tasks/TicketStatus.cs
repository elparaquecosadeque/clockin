using System.Text.Json.Serialization;

namespace TaskTickets.Api.Features.Tasks;

public enum TicketStatus
{
    [JsonStringEnumMemberName("todo")]
    Todo,
    [JsonStringEnumMemberName("in_progress")]
    InProgress,
    [JsonStringEnumMemberName("done")]
    Done
}
