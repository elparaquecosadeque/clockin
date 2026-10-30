namespace ClockIn.Domain.Common;

/// <summary>A business rule was broken. <see cref="Code"/> names the rule, as in the specs.</summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
