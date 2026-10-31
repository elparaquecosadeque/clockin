namespace ClockIn.Domain.Common;

/// <summary>An aggregate whose events the unit of work collects when it is saved.</summary>
public interface IRaisesDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
