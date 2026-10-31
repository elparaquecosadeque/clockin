using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;

namespace ClockIn.Domain.Tests.Teams;

/// <summary>create-team.feature: the rules the Team aggregate owns. Name uniqueness and "leads at most one team" need the repository.</summary>
public sealed class TeamTests
{
    [Fact]
    public void Creates_an_active_team_led_by_a_supervisor_with_a_trimmed_name()
    {
        var sid = User.Create(new Email("sid@clockin.test"), "Sid", Role.Supervisor);

        var team = Team.Create("  Night Shift ", sid);

        team.Name.Should().Be("Night Shift");
        team.SupervisorId.Should().Be(sid.Id);
        team.IsArchived.Should().BeFalse();
    }

    [Theory]
    [InlineData(Role.Employee)]
    [InlineData(Role.Admin)]
    public void Rejects_a_user_who_is_not_a_supervisor(Role role)
    {
        var eve = User.Create(new Email("eve@clockin.test"), "Eve", role);

        var act = () => Team.Create("Night Shift", eve);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsNotSupervisor");
    }

    [Fact]
    public void Rejects_a_deactivated_supervisor()
    {
        var rob = User.Create(new Email("rob@clockin.test"), "Rob", Role.Supervisor);
        rob.Deactivate();

        var act = () => Team.Create("Night Shift", rob);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsDeactivated");
    }

    [Theory]
    [InlineData("Warehouse")]
    [InlineData("warehouse")]
    [InlineData("  Warehouse ")]
    public void Compares_names_ignoring_case_and_surrounding_spaces(string name)
    {
        Team.NormalizeName(name).Should().Be(Team.NormalizeName("WAREHOUSE"));
    }
}
