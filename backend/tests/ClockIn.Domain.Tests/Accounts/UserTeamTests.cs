using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;

namespace ClockIn.Domain.Tests.Accounts;

/// <summary>move-employee.feature: the rules the User aggregate owns.</summary>
public sealed class UserTeamTests
{
    private readonly Team _warehouse = Team.Create("Warehouse", Supervisor("sam"));
    private readonly Team _office = Team.Create("Office", Supervisor("sue"));

    [Fact]
    public void Moves_an_employee_to_another_team()
    {
        User eve = Employee();

        eve.MoveTo(_office);

        eve.TeamId.Should().Be(_office.Id);
    }

    [Fact]
    public void Rejects_the_same_team()
    {
        User eve = Employee();

        var act = () => eve.MoveTo(_warehouse);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("AlreadyInTeam");
    }

    [Fact]
    public void Rejects_an_archived_team()
    {
        User eve = Employee();
        var oldStore = Team.Restore(TeamId.New(), "Old Store", UserId.New(), isArchived: true, version: 1);

        var act = () => eve.MoveTo(oldStore);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("TeamIsArchived");
    }

    [Theory]
    [InlineData(Role.Supervisor)]
    [InlineData(Role.Admin)]
    public void Rejects_users_who_are_not_employees(Role role)
    {
        var sam = User.Create(new Email("sam@clockin.test"), "Sam", role);

        var act = () => sam.MoveTo(_office);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsNotEmployee");
    }

    [Fact]
    public void Rejects_a_deactivated_employee()
    {
        User ed = Employee();
        ed.Deactivate();

        var act = () => ed.MoveTo(_office);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsDeactivated");
    }

    private User Employee()
    {
        var eve = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);
        eve.MoveTo(_warehouse);
        return eve;
    }

    private static User Supervisor(string name) => User.Create(new Email($"{name}@clockin.test"), name, Role.Supervisor);
}
