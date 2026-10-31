using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Common;

namespace ClockIn.Persistence.Common;

/// <summary>
/// One transaction per command. The subscribers are built in the command's scope, so they share its connection and
/// transaction: either everything is saved or nothing.
/// </summary>
public sealed class UnitOfWork(AppDataConnection db, PendingDomainEvents events, IEnumerable<IDomainEventSubscriber> subscribers)
    : IUnitOfWork
{
    private readonly IReadOnlyList<IDomainEventSubscriber> _subscribers = [.. subscribers];
    private bool _committed;

    public async Task BeginAsync(CancellationToken ct) => await db.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct)
    {
        // Subscribers may save aggregates that raise further events; those run too, before the commit.
        while (events.TryDequeue(out IDomainEvent domainEvent))
        {
            foreach (IDomainEventSubscriber subscriber in _subscribers)
            {
                await subscriber.HandleAsync(domainEvent, ct);
            }
        }

        await db.CommitTransactionAsync(ct);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed && db.Transaction is not null)
        {
            await db.RollbackTransactionAsync();
        }
    }
}
