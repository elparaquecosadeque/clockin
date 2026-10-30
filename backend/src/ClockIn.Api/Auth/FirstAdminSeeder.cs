using ClockIn.Application.Accounts.Commands;
using Wolverine;

namespace ClockIn.Api.Auth;

/// <summary>
/// seed-first-admin.feature: on startup, creates the first admin or logs a fresh link while it is still pending.
/// A hosted service because the mediator only works once the host has started; a failure here stops startup.
/// </summary>
public sealed class FirstAdminSeeder(IServiceScopeFactory scopes, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new SeedFirstAdminCommand(configuration["FirstAdmin:Email"]), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
