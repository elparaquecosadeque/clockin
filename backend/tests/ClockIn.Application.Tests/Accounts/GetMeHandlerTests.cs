using AwesomeAssertions;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Queries;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;
using NSubstitute;

namespace ClockIn.Application.Tests.Accounts;

/// <summary>The session's view of the signed-in user, which the frontend uses to show or hide features.</summary>
public sealed class GetMeHandlerTests
{
    [Theory]
    [InlineData(Role.Employee, new[] { Permissions.PunchTime })]
    [InlineData(Role.Supervisor, new[] { Permissions.PunchTime, Permissions.ReviewTeam })]
    [InlineData(Role.Admin, new[] { Permissions.ManageUsers, Permissions.ManageTeams, Permissions.ReviewAll, Permissions.ManageTimesheets })]
    public async Task Returns_the_user_with_the_permissions_of_their_role(Role role, string[] permissions)
    {
        var user = User.Create(new Email("eve@clockin.test"), "Eve", role);
        IUserRepository usersRepository = Substitute.For<IUserRepository>();
        usersRepository.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        ICurrentUser currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(user.Id);

        MeDto me = await new GetMeHandler(currentUser, usersRepository, Substitute.For<ITeamRepository>()).HandleAsync(new GetMeQuery(), CancellationToken.None);

        me.Should().BeEquivalentTo(new MeDto(user.Id.Value, "Eve", "eve@clockin.test", role, permissions, "en", "24h", null, null));
    }

    [Fact]
    public async Task Names_the_employees_team_and_its_supervisor()
    {
        var sam = User.Create(new Email("sam@clockin.test"), "Sam", Role.Supervisor);
        var team = Team.Create("Warehouse", sam);
        var eve = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);
        eve.MoveTo(team);
        IUserRepository usersRepository = Substitute.For<IUserRepository>();
        usersRepository.FindAsync(eve.Id, Arg.Any<CancellationToken>()).Returns(eve);
        usersRepository.FindAsync(sam.Id, Arg.Any<CancellationToken>()).Returns(sam);
        ITeamRepository teamsRepository = Substitute.For<ITeamRepository>();
        teamsRepository.FindAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        ICurrentUser currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(eve.Id);

        MeDto me = await new GetMeHandler(currentUser, usersRepository, teamsRepository).HandleAsync(new GetMeQuery(), CancellationToken.None);

        me.TeamName.Should().Be("Warehouse");
        me.SupervisorName.Should().Be("Sam");
    }
}
