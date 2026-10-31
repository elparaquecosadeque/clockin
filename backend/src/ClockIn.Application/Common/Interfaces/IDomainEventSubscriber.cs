using ClockIn.Domain.Common;

namespace ClockIn.Application.Common.Interfaces;

/// <summary>
/// Reacts to domain events inside the transaction of the command that raised them. Derive from
/// <see cref="Events.DomainEventSubscriber{TEvent}"/> rather than implementing this directly.
/// </summary>
public interface IDomainEventSubscriber
{
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct);
}
