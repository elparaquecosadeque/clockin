namespace ClockIn.Application.Common.Exceptions;

/// <summary>The request conflicts with the current state, including optimistic-concurrency failures.</summary>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
