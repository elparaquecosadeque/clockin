namespace ClockIn.Application.Common.Interfaces;

/// <summary>The transaction of one command. Disposing without committing rolls back.</summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken ct);

    /// <summary>Dispatches the domain events of the aggregates saved so far, and of their handlers, then commits.</summary>
    Task CommitAsync(CancellationToken ct);
}
