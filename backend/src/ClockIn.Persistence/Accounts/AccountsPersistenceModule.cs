using ClockIn.Application.Accounts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.Persistence.Accounts;

public static class AccountsPersistenceModule
{
    public static IServiceCollection AddAccountsPersistence(this IServiceCollection services) =>
        services
            .AddScoped<IUserRepository, UserRepository>()
            .AddScoped<ISessionRepository, SessionRepository>();
}
