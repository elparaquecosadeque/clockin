using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;
using ClockIn.Persistence.Common;
using LinqToDB;
using LinqToDB.Async;

namespace ClockIn.Persistence.Teams;

public sealed class TeamRepository(AppDataConnection db) : ITeamRepository
{
    public async Task<Team?> FindAsync(TeamId id, CancellationToken ct)
    {
        TeamRecord? record = await db.GetTable<TeamRecord>().FirstOrDefaultAsync(t => t.Id == id.Value, ct);
        return record is null ? null : ToDomain(record);
    }

    public Task<bool> NameInUseAsync(string name, CancellationToken ct)
    {
        string normalized = Team.NormalizeName(name);
        return db.GetTable<TeamRecord>().AnyAsync(t => t.NormalizedName == normalized, ct);
    }

    public Task<bool> LeadsActiveTeamAsync(UserId supervisorId, CancellationToken ct) =>
        db.GetTable<TeamRecord>().AnyAsync(t => t.SupervisorId == supervisorId.Value && !t.IsArchived, ct);

    public async Task SaveOrUpdateAsync(Team team, CancellationToken ct)
    {
        if (team.Version == 0)
        {
            try
            {
                await db.InsertAsync(ToRecord(team, version: 1), token: ct);
            }
            catch (Exception ex) when (UniqueViolation.Is(ex))
            {
                // Lost a race with the handler's checks: same name, or the supervisor got another team.
                throw new ConflictException("ConcurrencyConflict", "Another team was saved at the same time. Reload and try again.");
            }

            return;
        }

        int updated = await db.GetTable<TeamRecord>()
            .Where(t => t.Id == team.Id.Value && t.Version == team.Version)
            .Set(t => t.Name, team.Name)
            .Set(t => t.NormalizedName, Team.NormalizeName(team.Name))
            .Set(t => t.SupervisorId, team.SupervisorId.Value)
            .Set(t => t.IsArchived, team.IsArchived)
            .Set(t => t.Version, team.Version + 1)
            .UpdateAsync(ct);
        if (updated == 0)
        {
            throw new ConflictException("ConcurrencyConflict", "The team was changed by someone else. Reload and try again.");
        }
    }

    private static Team ToDomain(TeamRecord record) =>
        Team.Restore(new TeamId(record.Id), record.Name, new UserId(record.SupervisorId), record.IsArchived, record.Version);

    private static TeamRecord ToRecord(Team team, int version) => new()
    {
        Id = team.Id.Value,
        Name = team.Name,
        NormalizedName = Team.NormalizeName(team.Name),
        SupervisorId = team.SupervisorId.Value,
        IsArchived = team.IsArchived,
        Version = version,
    };
}
