using AwesomeAssertions;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Teams.Commands;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Application.Teams.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;
using FluentValidation.TestHelper;
using NSubstitute;

namespace ClockIn.Application.Tests.Teams;

/// <summary>create-team.feature, as the use case sees it. Only admins reach it (permission policy).</summary>
public sealed class CreateTeamHandlerTests
{
    private readonly IUserRepository _usersRepository = Substitute.For<IUserRepository>();
    private readonly ITeamRepository _teamsRepository = Substitute.For<ITeamRepository>();
    private readonly User _sid = User.Create(new Email("sid@clockin.test"), "Sid", Role.Supervisor);
    private readonly CreateTeamHandler _handler;

    public CreateTeamHandlerTests()
    {
        _usersRepository.FindAsync(_sid.Id, Arg.Any<CancellationToken>()).Returns(_sid);
        _handler = new CreateTeamHandler(_usersRepository, _teamsRepository);
    }

    [Fact]
    public async Task Creates_a_team_led_by_a_free_supervisor()
    {
        TeamDto team = await _handler.HandleAsync(new CreateTeamCommand(" Night Shift ", _sid.Id.Value), CancellationToken.None);

        team.Name.Should().Be("Night Shift");
        team.SupervisorId.Should().Be(_sid.Id.Value);
        team.IsArchived.Should().BeFalse();
        await _teamsRepository.Received(1).SaveOrUpdateAsync(Arg.Is<Team>(t => t.Name == "Night Shift"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_name_already_in_use_including_archived_teams()
    {
        _teamsRepository.NameInUseAsync(" warehouse", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => _handler.HandleAsync(new CreateTeamCommand(" warehouse", _sid.Id.Value), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("TeamNameInUse");
    }

    [Fact]
    public async Task Rejects_a_supervisor_who_already_leads_a_team()
    {
        _teamsRepository.LeadsActiveTeamAsync(_sid.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => _handler.HandleAsync(new CreateTeamCommand("Night Shift", _sid.Id.Value), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SupervisorAlreadyLeadsTeam");
    }

    [Fact]
    public async Task Rejects_an_unknown_supervisor()
    {
        var act = () => _handler.HandleAsync(new CreateTeamCommand("Night Shift", Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Fails_validation_on_a_blank_name(string name) =>
        new CreateTeamValidator().TestValidate(new CreateTeamCommand(name, Guid.CreateVersion7()))
            .ShouldHaveValidationErrorFor(c => c.Name);

    [Fact]
    public void Fails_validation_on_a_name_over_100_characters() =>
        new CreateTeamValidator().TestValidate(new CreateTeamCommand(new string('a', 101), Guid.CreateVersion7()))
            .ShouldHaveValidationErrorFor(c => c.Name);

    [Fact]
    public void Fails_validation_without_a_supervisor() =>
        new CreateTeamValidator().TestValidate(new CreateTeamCommand("Night Shift", null))
            .ShouldHaveValidationErrorFor(c => c.SupervisorId);
}
