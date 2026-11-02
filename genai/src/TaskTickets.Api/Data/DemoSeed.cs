using TaskTickets.Api.Features.Auth;
using TaskTickets.Api.Features.Tasks;

namespace TaskTickets.Api.Data;

public static class DemoSeed
{
    public static readonly Guid AliceId = Guid.Parse("00000000-0000-4000-8000-000000000001");
    public static readonly Guid BobId = Guid.Parse("00000000-0000-4000-8000-000000000002");

    public static void Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        auth.Register(new RegisterRequest("Alice", "alice@example.test", "DemoPassword123!"), AliceId);
        auth.Register(new RegisterRequest("Bob", "bob@example.test", "DemoPassword123!"), BobId);
        var store = scope.ServiceProvider.GetRequiredService<InMemoryStore>();
        store.AddTicket(new TaskTicket(Guid.Parse("00000000-0000-4000-8000-000000000101"), "Plan the demo", "Alice's personal ticket.",
            TicketStatus.Todo, new DateOnly(2026, 11, 1), AliceId, AliceId, new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)));
        store.AddTicket(new TaskTicket(Guid.Parse("00000000-0000-4000-8000-000000000102"), "Review the API", "Alice assigned this ticket to Bob.",
            TicketStatus.InProgress, new DateOnly(2026, 11, 2), AliceId, BobId, new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero)));
        store.AddTicket(new TaskTicket(Guid.Parse("00000000-0000-4000-8000-000000000103"), "Explore Scalar", "Bob's completed ticket.",
            TicketStatus.Done, null, BobId, BobId, new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero)));
    }
}
