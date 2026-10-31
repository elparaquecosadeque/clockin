using ClockIn.Application.Demo.Results;

namespace ClockIn.Application.Demo.Interfaces;

/// <summary>The demo company (ADR 0005). Only used in the Development and Demo environments.</summary>
public interface IDemoData
{
    /// <summary>Every demo account's password.</summary>
    string Password { get; }

    /// <summary>One account per scenario worth trying.</summary>
    IReadOnlyList<DemoAccount> FeaturedAccounts { get; }

    /// <summary>Null when the data was never seeded.</summary>
    Task<DateTimeOffset?> LastSeededAtAsync(CancellationToken ct);

    /// <summary>Wipes every table and rebuilds the demo company relative to now.</summary>
    /// <param name="passwordHash">The hash of <see cref="Password"/>.</param>
    /// <param name="ct"></param>
    Task ResetAsync(string passwordHash, CancellationToken ct);
}
