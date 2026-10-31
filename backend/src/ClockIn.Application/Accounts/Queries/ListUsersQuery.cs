using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Reporting.Interfaces;

namespace ClockIn.Application.Accounts.Queries;

/// <summary>Admins only (permission policy). Everyone, deactivated included, by name.</summary>
public sealed record ListUsersQuery;

public sealed class ListUsersHandler(IReportingReads reads, TimeProvider time)
{
    public async Task<IReadOnlyList<UserListItemDto>> HandleAsync(ListUsersQuery query, CancellationToken ct)
    {
        var teams = (await reads.TeamsAsync(ct)).ToDictionary(t => t.Id);
        ILookup<Guid, TeamRow> led = teams.Values.Where(t => !t.IsArchived).ToLookup(t => t.SupervisorId);
        DateTimeOffset now = time.GetUtcNow();
        return [.. (await reads.PeopleAsync(ct))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new UserListItemDto(
                p.Id,
                p.Name,
                p.Email,
                p.Role,
                p.Status,
                p.TeamId,
                p.TeamId is { } teamId ? teams[teamId].Name : null,
                led[p.Id].FirstOrDefault()?.Id,
                p.LockedUntil > now))];
    }
}
