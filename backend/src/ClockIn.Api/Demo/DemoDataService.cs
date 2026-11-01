using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Demo.Interfaces;
using Microsoft.Extensions.Options;

namespace ClockIn.Api.Demo;

/// <summary>
/// Seeds the demo data on startup when it was never seeded, then rebuilds it once it is 24 hours old,
/// checking every hour (ADR 0005). Startup waits for the first seed, so the first-admin seed after it
/// finds the demo admin and does nothing.
/// </summary>
public sealed partial class DemoDataService(
    IServiceScopeFactory scopes,
    IOptions<DemoOptions> options,
    TimeProvider time,
    ILogger<DemoDataService> logger)
    : IHostedService, IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan ResetInterval = TimeSpan.FromHours(24);

    private readonly CancellationTokenSource _stopping = new();
    private Task _loop = Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        await ResetIfDueAsync(cancellationToken);
        if (options.Value.DailyReset)
        {
            _loop = CheckPeriodicallyAsync(_stopping.Token);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        await _loop.WaitAsync(cancellationToken);
    }

    public void Dispose() => _stopping.Dispose();

    private async Task CheckPeriodicallyAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(CheckInterval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await ResetIfDueAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogResetFailed(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }
    }

    private async Task ResetIfDueAsync(CancellationToken ct)
    {
        using IServiceScope scope = scopes.CreateScope();
        IDemoData demo = scope.ServiceProvider.GetRequiredService<IDemoData>();
        DateTimeOffset? lastSeededAt = await demo.LastSeededAtAsync(ct);
        if (lastSeededAt is not null && (!options.Value.DailyReset || time.GetUtcNow() - lastSeededAt < ResetInterval))
        {
            return;
        }

        string passwordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(demo.Password);
        await demo.ResetAsync(passwordHash, ct);
        LogReset();
    }

    [LoggerMessage(LogLevel.Information, "Demo data rebuilt; every demo session was signed out.")]
    private partial void LogReset();

    [LoggerMessage(LogLevel.Error, "Rebuilding the demo data failed; the next check retries.")]
    private partial void LogResetFailed(Exception exception);
}
