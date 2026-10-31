namespace ClockIn.Api.Demo;

/// <summary>Demo data (ADR 0005). Only allowed in the Development and Demo environments.</summary>
public sealed class DemoOptions
{
    public const string Section = "Demo";

    public const string Environment = "Demo";

    public bool Enabled { get; init; }

    /// <summary>Rebuilds the data every 24 hours. Off: seeded once, then kept.</summary>
    public bool DailyReset { get; init; } = true;
}
