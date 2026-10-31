namespace ClockIn.Domain.Teams;

public readonly record struct TeamId(Guid Value)
{
    public static TeamId New() => new(Guid.CreateVersion7());
}
