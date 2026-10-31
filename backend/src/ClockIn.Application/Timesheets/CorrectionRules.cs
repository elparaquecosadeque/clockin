using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets;

public static class CorrectionRules
{
    /// <summary>A pending correction request blocks approving, returning and reopening until it is decided.</summary>
    public static async Task EnsureNonePendingAsync(this ICorrectionRequestRepository correctionsRepository, TimesheetId timesheetId, CancellationToken ct)
    {
        if (await correctionsRepository.FindPendingAsync(timesheetId, ct) is not null)
        {
            throw new DomainException("PendingCorrectionRequest", "Decide the pending correction request first.");
        }
    }

    /// <summary>The request, once the signed-in user is the owner's current approver.</summary>
    public static async Task<CorrectionRequest> GetForApproverAsync(
        this ICorrectionRequestRepository correctionsRepository, Guid requestId, ICurrentUser currentUser, IUserRepository usersRepository,
        ITeamRepository teamsRepository, CancellationToken ct)
    {
        CorrectionRequest request = await correctionsRepository.FindAsync(new CorrectionRequestId(requestId), ct)
            ?? throw new NotFoundException("Correction request not found.");
        TimesheetParties parties = await TimesheetParties.LoadAsync(usersRepository, teamsRepository, currentUser.Id, request.OwnerId, ct);
        TimesheetParties.Require(parties.IsApprover);
        return request;
    }
}
