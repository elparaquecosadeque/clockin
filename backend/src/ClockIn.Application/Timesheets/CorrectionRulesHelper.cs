using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Timesheets;

public static class CorrectionRulesHelper
{
    /// <summary>A pending correction request blocks approving, returning and reopening until it is decided.</summary>
    public static async Task EnsureNonePendingAsync(
        this ICorrectionRequestRepository correctionsRepository,
        TimesheetId timesheetId,
        CancellationToken ct)
    {
        if (await correctionsRepository.FindPendingAsync(timesheetId, ct) is not null)
        {
            throw new DomainException("PendingCorrectionRequest", "Decide the pending correction request first.");
        }
    }
}
