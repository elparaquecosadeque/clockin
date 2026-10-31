using AwesomeAssertions;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Accounts;

/// <summary>log-out.feature, as the use case sees it.</summary>
public sealed class LogOutHandlerTests
{
    private readonly CompanyClock _clock = new();
    private readonly ISessionRepository _sessionsRepository = Substitute.For<ISessionRepository>();
    private readonly LogOutHandler _handler;

    public LogOutHandlerTests() => _handler = new LogOutHandler(_sessionsRepository, _clock);

    [Fact]
    public async Task Revokes_only_the_session_of_the_given_token()
    {
        (Session phone, RefreshToken token) = Session.Start(UserId.New(), Guid.CreateVersion7(), _clock.GetUtcNow());
        _sessionsRepository.FindAsync(phone.Id, Arg.Any<CancellationToken>()).Returns(phone);

        await _handler.HandleAsync(new LogOutCommand(token.Value), CancellationToken.None);

        phone.RevokedAt.Should().Be(_clock.GetUtcNow());
        await _sessionsRepository.Received(1).SaveOrUpdateAsync(phone, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("malformed")]
    [InlineData("0193a1b2c3d47e8f9a0b1c2d3e4f5a6b.unknown-session")]
    public async Task Does_nothing_when_the_token_matches_no_session(string? token)
    {
        await _handler.HandleAsync(new LogOutCommand(token), CancellationToken.None);

        await _sessionsRepository.DidNotReceiveWithAnyArgs().SaveOrUpdateAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Leaves_the_session_alone_when_the_secret_is_not_current()
    {
        (Session phone, _) = Session.Start(UserId.New(), Guid.CreateVersion7(), _clock.GetUtcNow());
        _sessionsRepository.FindAsync(phone.Id, Arg.Any<CancellationToken>()).Returns(phone);

        await _handler.HandleAsync(new LogOutCommand($"{phone.Id.Value:N}.guessed"), CancellationToken.None);

        phone.RevokedAt.Should().BeNull();
    }
}
