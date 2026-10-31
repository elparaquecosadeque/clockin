using AwesomeAssertions;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Teams.Commands;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;
using NSubstitute;

namespace ClockIn.Application.Tests.Teams;

/// <summary>move-employee.feature, as the use case sees it. Only admins reach it (permission policy).</summary>
public sealed class MoveEmployeeHandlerTests
{
    private readonly IUserRepository _usersRepository = Substitute.For<IUserRepository>();
    private readonly ITeamRepository _teamsRepository = Substitute.For<ITeamRepository>();
    private readonly Team _office = Team.Create("Office", User.Create(new Email("sue@clockin.test"), "Sue", Role.Supervisor));
    private readonly User _eve = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);
    private readonly MoveEmployeeHandler _handler;

    public MoveEmployeeHandlerTests()
    {
        _usersRepository.FindAsync(_eve.Id, Arg.Any<CancellationToken>()).Returns(_eve);
        _teamsRepository.FindAsync(_office.Id, Arg.Any<CancellationToken>()).Returns(_office);
        _handler = new MoveEmployeeHandler(_usersRepository, _teamsRepository);
    }

    [Fact]
    public async Task Moves_the_employee_to_the_team()
    {
        await _handler.HandleAsync(new MoveEmployeeCommand(_eve.Id.Value, _office.Id.Value), CancellationToken.None);

        _eve.TeamId.Should().Be(_office.Id);
        await _usersRepository.Received(1).SaveOrUpdateAsync(_eve, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_unknown_team()
    {
        var act = () => _handler.HandleAsync(new MoveEmployeeCommand(_eve.Id.Value, Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Rejects_an_unknown_user()
    {
        var act = () => _handler.HandleAsync(new MoveEmployeeCommand(Guid.CreateVersion7(), _office.Id.Value), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
