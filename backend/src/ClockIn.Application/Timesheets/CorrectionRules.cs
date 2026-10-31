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
    public static async Task EnsureNonePendingAsync(this ICorrectionRequestRepository corrections, TimesheetId timesheetId, CancellationToken ct)
    {
        if (await corrections.FindPendingAsync(timesheetId, ct) is not null)
        {
            throw new DomainException("PendingCorrectionRequest", "Decide the pending correction request first.");
        }
    }

    /// <summary>The request, once the signed-in user is the owner's current approver.</summary>
    public static async Task<CorrectionRequest> GetForApproverAsync(
        this ICorrectionRequestRepository corrections, Guid requestId, ICurrentUser currentUser, IUserRepository users,
        ITeamRepository teams, CancellationToken ct)
    {
        CorrectionRequest request = await corrections.FindAsync(new CorrectionRequestId(requestId), ct)
            ?? throw new NotFoundException("Correction request not found.");
        TimesheetParties parties = await TimesheetParties.LoadAsync(users, teams, currentUser.Id, request.OwnerId, ct);
        TimesheetParties.Require(parties.IsApprover);
        return request;
    }
}
