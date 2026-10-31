using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Teams.Results;
using ClockIn.Domain.Accounts;

namespace ClockIn.Application.Teams.Queries;

/// <summary>Admins only (permission policy). Active teams first, then archived, each by name.</summary>
public sealed record ListTeamsQuery;

public sealed class ListTeamsHandler(IReportingReads reads)
{
    public async Task<IReadOnlyList<TeamListItemDto>> HandleAsync(ListTeamsQuery query, CancellationToken ct)
    {
        IReadOnlyList<PersonRow> people = await reads.PeopleAsync(ct);
        var names = people.ToDictionary(p => p.Id, p => p.Name);
        return [.. (await reads.TeamsAsync(ct))
            .OrderBy(t => t.IsArchived)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new TeamListItemDto(
                t.Id,
                t.Name,
                t.SupervisorId,
                names.GetValueOrDefault(t.SupervisorId, ""),
                t.IsArchived,
                people.Count(p => p.TeamId == t.Id && p.Status != UserStatus.Deactivated)))];
    }
}
