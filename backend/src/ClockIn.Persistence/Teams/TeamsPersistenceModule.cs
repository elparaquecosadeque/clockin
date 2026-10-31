using ClockIn.Application.Teams.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.Persistence.Teams;

public static class TeamsPersistenceModule
{
    public static IServiceCollection AddTeamsPersistence(this IServiceCollection services) =>
        services.AddScoped<ITeamRepository, TeamRepository>();
}
