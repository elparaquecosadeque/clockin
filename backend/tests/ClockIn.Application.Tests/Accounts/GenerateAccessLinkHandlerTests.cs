using AwesomeAssertions;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Accounts;

/// <summary>generate-access-link.feature, as the use case sees it. Only admins reach it (permission policy).</summary>
public sealed class GenerateAccessLinkHandlerTests
{
    private readonly CompanyClock _clock = new();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IAuditLog _audit = Substitute.For<IAuditLog>();
    private readonly UserId _ada = UserId.New();
    private readonly User _eve = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);
    private readonly GenerateAccessLinkHandler _handler;

    public GenerateAccessLinkHandlerTests()
    {
        _eve.SetPassword("hash");
        _users.FindAsync(_eve.Id, Arg.Any<CancellationToken>()).Returns(_eve);
        ICurrentUser currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(_ada);
        _handler = new GenerateAccessLinkHandler(currentUser, _users, _audit, _clock);
    }

    [Fact]
    public async Task Returns_a_link_valid_for_72_hours_and_audits_it_without_the_token()
    {
        AccessLinkDto link = await _handler.HandleAsync(new GenerateAccessLinkCommand(_eve.Id.Value), CancellationToken.None);

        link.ExpiresAt.Should().Be(_clock.GetUtcNow().AddHours(72));
        AccessLink.TryParse(link.Token, out UserId userId, out _).Should().BeTrue();
        userId.Should().Be(_eve.Id);
        await _users.Received(1).SaveAsync(_eve, Arg.Any<CancellationToken>());
        _audit.Received(1).AccessLinkGenerated(_ada, _eve.Id);
    }

    [Fact]
    public async Task Rejects_an_unknown_user()
    {
        var act = () => _handler.HandleAsync(new GenerateAccessLinkCommand(Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Rejects_a_deactivated_user()
    {
        _eve.Deactivate();

        var act = () => _handler.HandleAsync(new GenerateAccessLinkCommand(_eve.Id.Value), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("UserIsDeactivated");
    }
}
