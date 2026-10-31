using ClockIn.Application.Teams.Results;
using ClockIn.Domain.Teams;

namespace ClockIn.Application.Teams.Mappings;

public static class TeamMappings
{
    public static TeamDto ToDto(this Team team) => new(team.Id.Value, team.Name, team.SupervisorId.Value, team.IsArchived);
}
