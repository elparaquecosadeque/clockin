using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Reporting;

/// <summary>
/// Who the signed-in user reports on. Supervisors see the employees of the team they lead now; admins see everyone
/// who keeps time, supervisors included. Queues differ: an admin's queue holds the supervisors, since employees go to
/// their supervisor.
/// </summary>
public sealed class ReportingScope
{
    private readonly Dictionary<Guid, TeamRow> _teams;

    private ReportingScope(PersonRow actor, IReadOnlyList<PersonRow> people, IReadOnlyList<TeamRow> teams)
    {
        Actor = actor;
        People = people;
        _teams = teams.ToDictionary(t => t.Id);
        IsAdmin = actor.Role.Can(Permissions.ReviewAll);
        LedTeam = actor.Role.Can(Permissions.ReviewTeam) ? teams.FirstOrDefault(t => t.SupervisorId == actor.Id && !t.IsArchived) : null;
    }

    public PersonRow Actor { get; }

    public IReadOnlyList<PersonRow> People { get; }

    public bool IsAdmin { get; }

    /// <summary>The active team a supervisor leads; null for admins and for supervisors without a team.</summary>
    public TeamRow? LedTeam { get; }

    public bool CanReview => IsAdmin || Actor.Role.Can(Permissions.ReviewTeam);

    public IEnumerable<TeamRow> ActiveTeams => _teams.Values.Where(t => !t.IsArchived).OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Everyone whose time the user may see in reports.</summary>
    public IEnumerable<PersonRow> Reviewees => IsAdmin
        ? People.Where(p => p.Role.Can(Permissions.PunchTime))
        : People.Where(p => p.Role == Role.Employee && LedTeam is not null && p.TeamId == LedTeam.Id);

    /// <summary>Whose submissions and correction requests wait for this user.</summary>
    public IEnumerable<PersonRow> QueueOwners => IsAdmin
        ? People.Where(p => p.Role.Can(Permissions.PunchTime) && p.TeamId is null)
        : Reviewees;

    public static async Task<ReportingScope> LoadAsync(
        ICurrentUser currentUser,
        IReportingReads reads,
        CancellationToken ct)
    {
        IReadOnlyList<PersonRow> people = await reads.PeopleAsync(ct);
        PersonRow actor = people.FirstOrDefault(p => p.Id == currentUser.Id.Value) ?? throw new UnauthorizedException();
        return new ReportingScope(actor, people, await reads.TeamsAsync(ct));
    }

    public TeamRow? Team(Guid? id) => id is { } teamId ? _teams.GetValueOrDefault(teamId) : null;

    public void RequireReview()
    {
        if (!CanReview)
        {
            throw new ForbiddenException("Only supervisors and admins see reports.");
        }
    }

    /// <summary>The team a report is filtered to: a supervisor's own team only, any existing team for admins.</summary>
    public TeamRow? FilterTeam(Guid? teamId)
    {
        RequireReview();
        if (teamId is null)
        {
            return IsAdmin ? null : LedTeam;
        }

        TeamRow team = Team(teamId) ?? throw new NotFoundException("Team not found.");
        if (!IsAdmin && team.Id != LedTeam?.Id)
        {
            throw new ForbiddenException("Supervisors see only the team they lead.");
        }

        return team;
    }
}
