using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Infrastructure.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.Infrastructure.Common;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) =>
        services
            .AddSingleton<IPasswordHasher, PasswordHasher>()
            .AddSingleton<IAuditLog, AuditLog>();
}
