using AwesomeAssertions;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Reporting.Interfaces;
using ClockIn.Application.Teams.Commands;
using ClockIn.Application.Teams.Queries;
using ClockIn.Application.Teams.Results;
using ClockIn.Application.Tests.Timesheets;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Timesheets;
using NSubstitute;

namespace ClockIn.Application.Tests.Teams;

/// <summary>rename-team, change-team-supervisor and archive-team, as the use cases see them. Admins only (permission policy).</summary>
public sealed class TeamAdministrationHandlerTests
{
    private readonly TimesheetWorld _world = new();
    private readonly IReportingReads _reads = Substitute.For<IReportingReads>();
    private readonly List<PersonRow> _people = [];
    private readonly List<TimesheetRow> _timesheets = [];
    private readonly List<CorrectionRow> _corrections = [];

    public TeamAdministrationHandlerTests()
    {
        _world.TeamsRepository.LeadsActiveTeamAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<UserId>() == _world.Warehouse.SupervisorId || c.Arg<UserId>() == _world.Office.SupervisorId);
        _world.TeamsRepository.NameInUseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(c => new[] { "warehouse", "office" }.Contains(c.Arg<string>().Trim().ToLowerInvariant()));
        _reads.PeopleAsync(Arg.Any<CancellationToken>()).Returns(_ => _people);
        _reads.TimesheetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), null, Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyList<TimesheetRow>)[.. _timesheets.Where(t => c.Arg<IReadOnlyCollection<Guid>>().Contains(t.OwnerId))]);
        _reads.CorrectionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(c => (IReadOnlyList<CorrectionRow>)[.. _corrections.Where(r => c.Arg<IReadOnlyCollection<Guid>>().Contains(r.OwnerId))]);
    }

    private static CancellationToken Ct => CancellationToken.None;

    [Fact]
    public async Task Renames_and_allows_changing_only_the_case()
    {
        await Rename().HandleAsync(new RenameTeamCommand(_world.Warehouse.Id.Value, "WAREHOUSE"), Ct);

        _world.Warehouse.Name.Should().Be("WAREHOUSE");
        await _world.TeamsRepository.Received(1).SaveOrUpdateAsync(_world.Warehouse, Ct);
    }

    [Fact]
    public async Task Rejects_a_name_used_by_another_team()
    {
        var act = () => Rename().HandleAsync(new RenameTeamCommand(_world.Warehouse.Id.Value, "office"), Ct);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("TeamNameInUse");
    }

    [Fact]
    public async Task Hands_the_team_to_a_free_supervisor()
    {
        User sid = Supervisor("Sid");

        await ChangeSupervisor().HandleAsync(new ChangeTeamSupervisorCommand(_world.Warehouse.Id.Value, sid.Id.Value), Ct);

        _world.Warehouse.SupervisorId.Should().Be(sid.Id);
    }

    [Fact]
    public async Task Rejects_a_supervisor_who_already_leads_a_team_including_the_current_one()
    {
        var sue = () => ChangeSupervisor().HandleAsync(new ChangeTeamSupervisorCommand(_world.Warehouse.Id.Value, _world.Sue.Id.Value), Ct);
        var sam = () => ChangeSupervisor().HandleAsync(new ChangeTeamSupervisorCommand(_world.Warehouse.Id.Value, _world.Sam.Id.Value), Ct);

        (await sue.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SupervisorAlreadyLeadsTeam");
        (await sam.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SupervisorAlreadyLeadsTeam");
    }

    [Fact]
    public async Task Archives_a_team_whose_members_are_all_deactivated()
    {
        Member(UserStatus.Deactivated);

        await Archive().HandleAsync(new ArchiveTeamCommand(_world.Warehouse.Id.Value), Ct);

        _world.Warehouse.IsArchived.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.PendingActivation)]
    public async Task Rejects_a_team_with_active_employees(UserStatus status)
    {
        Member(status);

        var act = () => Archive().HandleAsync(new ArchiveTeamCommand(_world.Warehouse.Id.Value), Ct);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("TeamHasActiveEmployees");
    }

    [Fact]
    public async Task Rejects_a_team_with_pending_reviews()
    {
        PersonRow rob = Member(UserStatus.Deactivated);
        _timesheets.Add(new TimesheetRow(Guid.CreateVersion7(), rob.Id, new DateOnly(2026, 10, 19), TimesheetStatus.Submitted, null, null, null, 0, []));

        var submitted = () => Archive().HandleAsync(new ArchiveTeamCommand(_world.Warehouse.Id.Value), Ct);
        (await submitted.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("TeamHasPendingReviews");

        _timesheets.Clear();
        _corrections.Add(new CorrectionRow(
            Guid.CreateVersion7(), Guid.CreateVersion7(), rob.Id, new DateOnly(2026, 10, 12), CorrectionStatus.Pending, "missing", default, null));
        var pending = () => Archive().HandleAsync(new ArchiveTeamCommand(_world.Warehouse.Id.Value), Ct);
        (await pending.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("TeamHasPendingReviews");
    }

    [Fact]
    public async Task Lists_active_teams_first_with_their_supervisor_and_headcount()
    {
        _reads.TeamsAsync(Arg.Any<CancellationToken>()).Returns([
            new TeamRow(Guid.CreateVersion7(), "Archived", _world.Sam.Id.Value, true),
            new TeamRow(_world.Warehouse.Id.Value, "Warehouse", _world.Sam.Id.Value, false),
        ]);
        _people.Add(new PersonRow(_world.Sam.Id.Value, "Sam", "sam@clockin.test", Role.Supervisor, UserStatus.Active, null));
        Member(UserStatus.Active);
        Member(UserStatus.Deactivated);

        IReadOnlyList<TeamListItemDto> teams = await new ListTeamsHandler(_reads).HandleAsync(new ListTeamsQuery(), Ct);

        teams.Select(t => (t.Name, t.SupervisorName, t.EmployeeCount)).Should().Equal(("Warehouse", "Sam", 1), ("Archived", "Sam", 0));
    }

    private RenameTeamHandler Rename() => new(_world.TeamsRepository);

    private ChangeTeamSupervisorHandler ChangeSupervisor() => new(_world.UsersRepository, _world.TeamsRepository);

    private ArchiveTeamHandler Archive() => new(_world.TeamsRepository, _reads);

    private PersonRow Member(UserStatus status)
    {
        var person = new PersonRow(Guid.CreateVersion7(), "Rob", "rob@clockin.test", Role.Employee, status, _world.Warehouse.Id.Value);
        _people.Add(person);
        return person;
    }

    private User Supervisor(string name)
    {
        var user = User.Create(new Email($"{name.ToLowerInvariant()}@clockin.test"), name, Role.Supervisor);
        _world.UsersRepository.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }
}
