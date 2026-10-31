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
        this ITimesheetRepository timesheets, UserId ownerId, Week week, CancellationToken ct)
    {
        Timesheet?[] found =
        [
            await timesheets.FindAsync(ownerId, new Week(week.Start.AddDays(-7)), ct),
            await timesheets.FindAsync(ownerId, new Week(week.Start.AddDays(7)), ct),
            await timesheets.FindWithOpenEntryAsync(ownerId, ct),
        ];
        return [.. found.OfType<Timesheet>()];
    }

    /// <summary>The timesheet holding the entry, once the signed-in user may change it and has seen its latest version.</summary>
    public static async Task<Timesheet> ForEntryChangeAsync(
        this ITimesheetRepository timesheets, ICurrentUser currentUser, IUserRepository users, ITeamRepository teams,
        TimeEntryId entryId, int version, CancellationToken ct)
    {
        Timesheet timesheet = await timesheets.FindByEntryAsync(entryId, ct) ?? throw new NotFoundException("Entry not found.");
        TimesheetParties parties = await TimesheetParties.LoadAsync(users, teams, currentUser.Id, timesheet.OwnerId, ct);
        TimesheetParties.Require(parties.CanChange);
        if (timesheet.Version != version)
        {
            throw new ConflictException("ConcurrencyConflict", "The timesheet was changed by someone else. Reload and try again.");
        }

        return timesheet;
    }

    public static async Task<Timesheet> GetAsync(this ITimesheetRepository timesheets, Guid id, CancellationToken ct) =>
        await timesheets.FindAsync(new TimesheetId(id), ct) ?? throw new NotFoundException("Timesheet not found.");
}
