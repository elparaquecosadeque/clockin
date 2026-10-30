using ClockIn.Application.Timesheets.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.Persistence.Timesheets;

public static class TimesheetsPersistenceModule
{
    public static IServiceCollection AddTimesheetsPersistence(this IServiceCollection services) =>
        services.AddScoped<ITimesheetRepository, TimesheetRepository>();
}
