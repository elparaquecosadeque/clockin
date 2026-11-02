namespace TaskTickets.Api.Tests;

public sealed class TestTimeProvider : TimeProvider
{
    private long _ticks = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero).Ticks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
}
