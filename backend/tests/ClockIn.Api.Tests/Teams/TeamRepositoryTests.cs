using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Teams.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Teams;

namespace ClockIn.Api.Tests.Teams;

[Collection(SharedApi.Name)]
public sealed class TeamRepositoryTests(ApiFactory factory) : IDisposable
{
    private readonly Scopes _scopes = new(factory);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _scopes.Dispose();

    [Fact]
    public async Task Loads_what_was_saved()
    {
        var team = Team.Create(UniqueName(), await SupervisorAsync());
        await Teams().SaveOrUpdateAsync(team, Ct);

        Team? loaded = await Teams().FindAsync(team.Id, Ct);

        loaded.Should().BeEquivalentTo(team, o => o.Excluding(t => t.Version));
        loaded!.Version.Should().Be(1);
    }

    [Fact]
    public async Task Finds_a_name_in_use_ignoring_case_and_spaces()
    {
        string name = UniqueName();
        await Teams().SaveOrUpdateAsync(Team.Create(name, await SupervisorAsync()), Ct);

        (await Teams().NameInUseAsync($"  {name.ToLowerInvariant()} ", Ct)).Should().BeTrue();
        (await Teams().NameInUseAsync(UniqueName(), Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Knows_who_leads_an_active_team_and_keeps_it_to_one_under_concurrency()
    {
        User sid = await SupervisorAsync();
        await Teams().SaveOrUpdateAsync(Team.Create(UniqueName(), sid), Ct);

        (await Teams().LeadsActiveTeamAsync(sid.Id, Ct)).Should().BeTrue();
        var second = () => Teams().SaveOrUpdateAsync(Team.Create(UniqueName(), sid), Ct);
        (await second.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("ConcurrencyConflict");
    }

    [Fact]
    public async Task Keeps_an_employees_team()
    {
        var team = Team.Create(UniqueName(), await SupervisorAsync());
        await Teams().SaveOrUpdateAsync(team, Ct);
        var eve = User.Create(new Email($"{Guid.CreateVersion7():N}@clockin.test"), "Eve", Role.Employee);
        eve.MoveTo(team);

        await Users().SaveOrUpdateAsync(eve, Ct);

        (await Users().FindAsync(eve.Id, Ct))!.TeamId.Should().Be(team.Id);
    }

    private async Task<User> SupervisorAsync()
    {
        var sid = User.Create(new Email($"{Guid.CreateVersion7():N}@clockin.test"), "Sid", Role.Supervisor);
        await Users().SaveOrUpdateAsync(sid, Ct);
        return sid;
    }

    private static string UniqueName() => $"Team {Guid.CreateVersion7():N}";

    private ITeamRepository Teams() => _scopes.Get<ITeamRepository>();

    private IUserRepository Users() => _scopes.Get<IUserRepository>();
}
