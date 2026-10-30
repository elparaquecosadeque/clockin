using AwesomeAssertions;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Accounts;

/// <summary>refresh-session.feature, as the use case sees it.</summary>
public sealed class RefreshSessionHandlerTests
{
    private readonly CompanyClock _clock = new("2026-10-30 08:00");
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IAuditLog _audit = Substitute.For<IAuditLog>();
    private readonly RefreshSessionHandler _handler;
    private readonly User _eve = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);
    private readonly Session _session;
    private readonly RefreshToken _token;

    public RefreshSessionHandlerTests()
    {
        _eve.SetPassword("hash");
        _users.FindAsync(_eve.Id, Arg.Any<CancellationToken>()).Returns(_eve);
        (_session, _token) = Session.Start(_eve.Id, _eve.SecurityStamp, _clock.GetUtcNow());
        _sessions.FindAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);
        _handler = new RefreshSessionHandler(_users, _sessions, _audit, _clock);
    }

    [Fact]
    public async Task Rotates_the_refresh_token_when_it_is_current()
    {
        _clock.Set("2026-10-30 19:00");

        SessionResult result = await _handler.HandleAsync(new RefreshSessionCommand(_token.Value), CancellationToken.None);

        result.UserId.Should().Be(_eve.Id.Value);
        result.RefreshToken.Should().NotBe(_token.Value);
        result.RefreshTokenExpiresAt.Should().Be(_clock.GetUtcNow().AddHours(12));
        await _sessions.Received(1).SaveAsync(_session, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("0193a1b2c3d47e8f9a0b1c2d3e4f5a6b.unknown-session")]
    public async Task Rejects_a_token_that_matches_no_session(string token)
    {
        var act = () => _handler.HandleAsync(new RefreshSessionCommand(token), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Rejects_an_expired_token()
    {
        _clock.Set("2026-10-30 20:00:01");

        var act = () => _handler.HandleAsync(new RefreshSessionCommand(_token.Value), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Revokes_every_session_of_the_user_when_a_rotated_token_is_reused()
    {
        await _handler.HandleAsync(new RefreshSessionCommand(_token.Value), CancellationToken.None);

        var act = () => _handler.HandleAsync(new RefreshSessionCommand(_token.Value), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        await _sessions.Received(1).RevokeAllAsync(_eve.Id, _clock.GetUtcNow(), Arg.Any<CancellationToken>());
        _audit.Received(1).RefreshTokenReused(_eve.Id);
    }

    [Fact]
    public async Task Rejects_the_session_when_the_password_was_reset()
    {
        _eve.SetPassword("new hash");

        var act = () => _handler.HandleAsync(new RefreshSessionCommand(_token.Value), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        await _sessions.DidNotReceiveWithAnyArgs().SaveAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Rejects_the_session_when_the_user_was_deactivated()
    {
        _eve.Deactivate();

        var act = () => _handler.HandleAsync(new RefreshSessionCommand(_token.Value), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
