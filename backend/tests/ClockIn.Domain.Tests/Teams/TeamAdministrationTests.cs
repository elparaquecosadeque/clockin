using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;

namespace ClockIn.Domain.Tests.Teams;

/// <summary>rename-team, change-team-supervisor and archive-team: the rules the Team owns.</summary>
public sealed class TeamAdministrationTests
{
    private readonly User _sam = User.Create(new Email("sam@clockin.test"), "Sam", Role.Supervisor);
    private readonly User _sid = User.Create(new Email("sid@clockin.test"), "Sid", Role.Supervisor);
    private readonly Team _warehouse;

    public TeamAdministrationTests() => _warehouse = Team.Create("Warehouse", _sam);

    [Fact]
    public void Renames_and_allows_changing_only_the_case()
    {
        _warehouse.Rename(" WAREHOUSE ");

        _warehouse.Name.Should().Be("WAREHOUSE");
    }

    [Fact]
    public void Hands_the_team_to_another_supervisor()
    {
        _warehouse.ChangeSupervisor(_sid);

        _warehouse.SupervisorId.Should().Be(_sid.Id);
    }

    [Fact]
    public void Rejects_a_supervisor_who_cannot_lead()
    {
        var eve = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);
        _sid.Deactivate();

        var employee = () => _warehouse.ChangeSupervisor(eve);
        var deactivated = () => _warehouse.ChangeSupervisor(_sid);

        employee.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsNotSupervisor");
        deactivated.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsDeactivated");
    }

    [Fact]
    public void Freezes_an_archived_team()
    {
        _warehouse.Archive();

        _warehouse.IsArchived.Should().BeTrue();
        foreach (Action act in new Action[] { () => _warehouse.Archive(), () => _warehouse.Rename("Older"), () => _warehouse.ChangeSupervisor(_sid) })
        {
            act.Should().Throw<DomainException>().Which.Code.Should().Be("TeamIsArchived");
        }
    }
}
