using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets;

/// <summary>Loading shared by the timesheet commands.</summary>
public static class TimesheetRepositoryExtensions
{
    /// <summary>The owner's timesheets an entry of <paramref name="week"/> could overlap: entries last at most 16 hours.</summary>
    public static async Task<IReadOnlyList<Timesheet>> NeighboursAsync(
        this ITimesheetRepository timesheetsRepository,
        UserId ownerId,
        Week week,
        CancellationToken ct)
    {
        // Sequential on purpose: parallelizing with Task.WhenAll is not recommended. The lookups share the request's
        // single database connection, which runs one command at a time and isn't thread-safe. If this ever gets
        // slow, load the three timesheets in one query instead.
        Timesheet?[] found =
        [
            await timesheetsRepository.FindAsync(ownerId, new Week(week.Start.AddDays(-7)), ct),
            await timesheetsRepository.FindAsync(ownerId, new Week(week.Start.AddDays(7)), ct),
            await timesheetsRepository.FindWithOpenEntryAsync(ownerId, ct),
        ];
        return [.. found.OfType<Timesheet>()];
    }

    public static async Task<Timesheet> GetAsync(
        this ITimesheetRepository timesheetsRepository,
        Guid id,
        CancellationToken ct) =>
        await timesheetsRepository.FindAsync(new TimesheetId(id), ct) ?? throw new NotFoundException("Timesheet not found.");
}
