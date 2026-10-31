using AwesomeAssertions;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Queries;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
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
        IUserRepository users = Substitute.For<IUserRepository>();
        users.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        ICurrentUser currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(user.Id);

        MeDto me = await new GetMeHandler(currentUser, users).HandleAsync(new GetMeQuery(), CancellationToken.None);

        me.Should().BeEquivalentTo(new MeDto(user.Id.Value, "Eve", "eve@clockin.test", role, permissions, "en", "24h"));
    }
}
