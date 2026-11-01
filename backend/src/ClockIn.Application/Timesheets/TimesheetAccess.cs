using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets;

/// <summary>What the signed-in user may do on a timesheet, shared by the timesheet handlers.</summary>
public sealed class TimesheetAccess(
    ICurrentUser currentUser,
    IUserRepository usersRepository,
    ITeamRepository teamsRepository)
{
    /// <summary>The signed-in user and the timesheet owner, with the owner's current team.</summary>
    public async Task<TimesheetParties> PartiesAsync(UserId ownerId, CancellationToken ct)
    {
        UserId actorId = currentUser.Id;
        var actor = await usersRepository.FindAsync(actorId, ct) ?? throw new UnauthorizedException();
        var owner = actorId == ownerId ? actor : await usersRepository.FindAsync(ownerId, ct) ?? throw new NotFoundException("User not found.");
        Team? team = owner.TeamId is { } teamId ? await teamsRepository.FindAsync(teamId, ct) : null;
        return new TimesheetParties(actor, owner, team);
    }

    /// <summary>The timesheet holding the entry, once the signed-in user may change it and has seen its latest version.</summary>
    public async Task<Timesheet> ForEntryChangeAsync(
        ITimesheetRepository timesheetsRepository,
        TimeEntryId entryId,
        int version,
        CancellationToken ct)
    {
        Timesheet timesheet = await timesheetsRepository.FindByEntryAsync(entryId, ct) ?? throw new NotFoundException("Entry not found.");
        TimesheetParties.Require((await PartiesAsync(timesheet.OwnerId, ct)).CanChange);
        if (timesheet.Version != version)
        {
            throw new ConflictException("ConcurrencyConflict", "The timesheet was changed by someone else. Reload and try again.");
        }

        return timesheet;
    }

    /// <summary>The request, once the signed-in user is the owner's current approver.</summary>
    public async Task<CorrectionRequest> ForApproverAsync(
        ICorrectionRequestRepository correctionsRepository,
        Guid requestId,
        CancellationToken ct)
    {
        CorrectionRequest request = await correctionsRepository.FindAsync(new CorrectionRequestId(requestId), ct)
            ?? throw new NotFoundException("Correction request not found.");
        TimesheetParties.Require((await PartiesAsync(request.OwnerId, ct)).IsApprover);
        return request;
    }
}
