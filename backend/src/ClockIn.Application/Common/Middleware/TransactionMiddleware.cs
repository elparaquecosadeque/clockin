using ClockIn.Application.Common.Interfaces;

namespace ClockIn.Application.Common.Middleware;

/// <summary>Mediator middleware for <see cref="ITransactional"/> commands: one transaction, events included.</summary>
public static class TransactionMiddleware
{
    public static Task BeforeAsync(IUnitOfWork unitOfWork, CancellationToken ct) => unitOfWork.BeginAsync(ct);

    public static Task AfterAsync(IUnitOfWork unitOfWork, CancellationToken ct) => unitOfWork.CommitAsync(ct);

    public static ValueTask FinallyAsync(IUnitOfWork unitOfWork) => unitOfWork.DisposeAsync();
}
