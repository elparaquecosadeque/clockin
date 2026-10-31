using ClockIn.Domain.Common;

namespace ClockIn.Persistence.Common;

/// <summary>The domain events of the aggregates saved in the current scope, waiting for the unit of work.</summary>
public sealed class PendingDomainEvents
{
    private readonly Queue<IDomainEvent> _events = new();

    public void CollectFrom(IRaisesDomainEvents aggregate)
    {
        foreach (IDomainEvent domainEvent in aggregate.DomainEvents)
        {
            _events.Enqueue(domainEvent);
        }

        aggregate.ClearDomainEvents();
    }

    public bool TryDequeue(out IDomainEvent domainEvent) => _events.TryDequeue(out domainEvent!);
}
