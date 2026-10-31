using AwesomeAssertions;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Accounts;

/// <summary>log-in.feature, as the use case sees it.</summary>
public sealed class LogInHandlerTests
{
    private const string Password = "correct horse battery";

    private readonly CompanyClock _clock = new();
    private readonly IUserRepository _usersRepository = Substitute.For<IUserRepository>();
    private readonly ISessionRepository _sessionsRepository = Substitute.For<ISessionRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IAuditLog _audit = Substitute.For<IAuditLog>();
    private readonly LogInHandler _handler;
    private readonly User _eve = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);

    public LogInHandlerTests()
    {
        _eve.SetPassword("hash-of-password");
        _hasher.Verify(Arg.Any<string?>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string?>(0) == "hash-of-password" && call.ArgAt<string>(1) == Password);
        _usersRepository.FindByEmailAsync(new Email("eve@clockin.test"), Arg.Any<CancellationToken>()).Returns(_eve);
        _handler = new LogInHandler(_usersRepository, _sessionsRepository, _hasher, _audit, _clock);
    }

    [Fact]
    public async Task Starts_a_session_when_the_credentials_are_correct()
    {
        SessionResult result = await _handler.HandleAsync(new LogInCommand("eve@clockin.test", Password), CancellationToken.None);

        result.UserId.Should().Be(_eve.Id.Value);
        result.SecurityStamp.Should().Be(_eve.SecurityStamp);
        result.RefreshTokenExpiresAt.Should().Be(_clock.GetUtcNow().AddHours(12));
        await _sessionsRepository.Received(1).SaveOrUpdateAsync(
            Arg.Is<Session>(s => s.UserId == _eve.Id && s.SecurityStamp == _eve.SecurityStamp),
            Arg.Any<CancellationToken>());
        _audit.Received(1).SignedIn(_eve.Id);
    }

    [Fact]
    public async Task Signs_in_when_the_email_differs_only_in_case()
    {
        SessionResult result = await _handler.HandleAsync(new LogInCommand("EVE@ClockIn.Test", Password), CancellationToken.None);

        result.UserId.Should().Be(_eve.Id.Value);
    }

    [Fact]
    public async Task Rejects_and_counts_the_failure_when_the_password_is_wrong()
    {
        var act = () => _handler.HandleAsync(new LogInCommand("eve@clockin.test", "wrong password!"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("InvalidCredentials");
        _eve.FailedSignIns.Should().Be(1);
        await _usersRepository.Received(1).SaveOrUpdateAsync(_eve, Arg.Any<CancellationToken>());
        _audit.Received(1).SignInFailed("eve@clockin.test");
        await _sessionsRepository.DidNotReceiveWithAnyArgs().SaveOrUpdateAsync(default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("nobody@clockin.test")]
    [InlineData("not-an-email")]
    public async Task Rejects_the_same_way_and_still_hashes_when_no_user_has_the_email(string email)
    {
        var act = () => _handler.HandleAsync(new LogInCommand(email, Password), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("InvalidCredentials");
        _hasher.Received(1).Verify(null, Password);
        _audit.Received(1).SignInFailed(email);
    }

    [Fact]
    public async Task Rejects_the_same_way_when_the_user_is_pending_activation()
    {
        var fay = User.Create(new Email("fay@clockin.test"), "Fay", Role.Employee);
        _usersRepository.FindByEmailAsync(fay.Email, Arg.Any<CancellationToken>()).Returns(fay);

        var act = () => _handler.HandleAsync(new LogInCommand("fay@clockin.test", Password), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("InvalidCredentials");
    }

    [Fact]
    public async Task Audits_the_lockout_when_the_fifth_attempt_fails()
    {
        for (int i = 0; i < 4; i++)
        {
            _eve.SignIn(passwordMatches: false, _clock.GetUtcNow());
        }

        var act = () => _handler.HandleAsync(new LogInCommand("eve@clockin.test", "wrong password!"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _audit.Received(1).LockedOut(_eve.Id);
        _eve.LockedUntil.Should().Be(_clock.GetUtcNow().AddMinutes(15));
    }
}
