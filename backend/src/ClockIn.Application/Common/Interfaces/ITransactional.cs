namespace ClockIn.Application.Common.Interfaces;

/// <summary>
/// Marks a command that runs in one transaction with its domain events (<see cref="Middleware.TransactionMiddleware"/>).
/// Commands that must keep a save even when they then fail, like a failed sign-in's counter, don't opt in.
/// </summary>
public interface ITransactional;
