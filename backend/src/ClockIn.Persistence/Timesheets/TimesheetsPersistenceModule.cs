using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Persistence.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.Persistence.Timesheets;

public static class TimesheetsPersistenceModule
{
    public static IServiceCollection AddTimesheetsPersistence(this IServiceCollection services) =>
        services
            .AddScoped<ITimesheetRepository, TimesheetRepository>()
            .AddScoped<ICorrectionRequestRepository, CorrectionRequestRepository>()
            .AddScoped<IReportingReads, ReportingReads>();
}
