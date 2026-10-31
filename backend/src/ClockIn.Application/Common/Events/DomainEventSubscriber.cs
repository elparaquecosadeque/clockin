using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Common;

namespace ClockIn.Application.Common.Events;

/// <summary>Reacts to one event type; every other event passes by.</summary>
public abstract class DomainEventSubscriber<TEvent> : IDomainEventSubscriber
    where TEvent : IDomainEvent
{
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct) =>
        domainEvent is TEvent typed ? HandleAsync(typed, ct) : Task.CompletedTask;

    protected abstract Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
