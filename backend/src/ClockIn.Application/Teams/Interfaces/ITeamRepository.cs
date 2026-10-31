using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;

namespace ClockIn.Application.Teams.Interfaces;

public interface ITeamRepository
{
    Task<Team?> FindAsync(TeamId id, CancellationToken ct);

    /// <summary>Compares with <see cref="Team.NormalizeName"/>, archived teams included.</summary>
    Task<bool> NameInUseAsync(string name, CancellationToken ct);

    /// <summary>Whether the user leads a team that isn't archived.</summary>
    Task<bool> LeadsActiveTeamAsync(UserId supervisorId, CancellationToken ct);

    /// <summary>Inserts or updates the whole aggregate.</summary>
    Task SaveOrUpdateAsync(Team team, CancellationToken ct);
}
