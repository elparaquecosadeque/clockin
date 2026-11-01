using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets;

/// <summary>Loading shared by the timesheet commands.</summary>
public static class TimesheetLoading
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

    /// <summary>The timesheet holding the entry, once the signed-in user may change it and has seen its latest version.</summary>
    public static async Task<Timesheet> ForEntryChangeAsync(
        this ITimesheetRepository timesheetsRepository,
        ICurrentUser currentUser,
        IUserRepository usersRepository,
        ITeamRepository teamsRepository,
        TimeEntryId entryId,
        int version,
        CancellationToken ct)
    {
        var timesheet = await timesheetsRepository.FindByEntryAsync(entryId, ct) ?? throw new NotFoundException("Entry not found.");
        var parties = await TimesheetParties.LoadAsync(usersRepository, teamsRepository, currentUser.Id, timesheet.OwnerId, ct);
        TimesheetParties.Require(parties.CanChange);
        if (timesheet.Version != version)
        {
            throw new ConflictException("ConcurrencyConflict", "The timesheet was changed by someone else. Reload and try again.");
        }

        return timesheet;
    }

    public static async Task<Timesheet> GetAsync(
        this ITimesheetRepository timesheetsRepository,
        Guid id,
        CancellationToken ct) =>
        await timesheetsRepository.FindAsync(new TimesheetId(id), ct) ?? throw new NotFoundException("Timesheet not found.");
}
