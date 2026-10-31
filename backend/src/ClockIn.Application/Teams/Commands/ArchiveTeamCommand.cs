using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;
using ClockIn.Domain.Timesheets;

namespace ClockIn.Application.Teams.Commands;

/// <summary>Admins only (permission policy). Frees the supervisor; history keeps the team's name.</summary>
public sealed record ArchiveTeamCommand(Guid TeamId);

public sealed class ArchiveTeamHandler(ITeamRepository teams, IReportingReads reads)
{
    public async Task HandleAsync(ArchiveTeamCommand command, CancellationToken ct)
    {
        Team team = await teams.FindAsync(new TeamId(command.TeamId), ct) ?? throw new NotFoundException("Team not found.");
        List<PersonRow> members = [.. (await reads.PeopleAsync(ct)).Where(p => p.TeamId == team.Id.Value)];
        if (members.Exists(p => p.Status != UserStatus.Deactivated))
        {
            throw new DomainException("TeamHasActiveEmployees", "Move or deactivate the team's employees first.");
        }

        Guid[] ids = [.. members.Select(p => p.Id)];
        if ((await reads.TimesheetsAsync(ids, null, ct)).Any(t => t.Status == TimesheetStatus.Submitted)
            || (await reads.CorrectionsAsync(ids, ct)).Any(r => r.Status == CorrectionStatus.Pending))
        {
            throw new DomainException("TeamHasPendingReviews", "Approve or return the team's pending timesheets and corrections first.");
        }

        team.Archive();
        await teams.SaveAsync(team, ct);
    }
}
