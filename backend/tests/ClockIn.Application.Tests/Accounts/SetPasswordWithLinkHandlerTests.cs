using AwesomeAssertions;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Tests.Common;
using FluentValidation.TestHelper;
using NSubstitute;

namespace ClockIn.Application.Tests.Accounts;

/// <summary>set-password-with-link.feature, as the use case sees it.</summary>
public sealed class SetPasswordWithLinkHandlerTests
{
    private readonly CompanyClock _clock = new();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly SetPasswordWithLinkHandler _handler;
    private readonly User _fay = User.Create(new Email("fay@clockin.test"), "Fay", Role.Employee);

    public SetPasswordWithLinkHandlerTests()
    {
        _users.FindAsync(_fay.Id, Arg.Any<CancellationToken>()).Returns(_fay);
        _hasher.Hash(Arg.Any<string>()).Returns(call => $"hashed:{call.Arg<string>()}");
        _handler = new SetPasswordWithLinkHandler(_users, _hasher, _clock);
    }

    [Fact]
    public async Task Activates_a_pending_account_with_the_hashed_password()
    {
        AccessLink link = _fay.IssueAccessLink(_clock.GetUtcNow());

        await _handler.HandleAsync(new SetPasswordWithLinkCommand(link.Token, "correct horse battery"), CancellationToken.None);

        _fay.Status.Should().Be(UserStatus.Active);
        _fay.PasswordHash.Should().Be("hashed:correct horse battery");
        await _users.Received(1).SaveAsync(_fay, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("0193a1b2c3d47e8f9a0b1c2d3e4f5a6b.no-such-user")]
    public async Task Rejects_a_token_that_matches_no_user_with_the_same_rule(string token)
    {
        var act = () => _handler.HandleAsync(new SetPasswordWithLinkCommand(token, "correct horse battery"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("InvalidOrExpiredLink");
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Checks_the_password_length(int length, bool valid)
    {
        TestValidationResult<SetPasswordWithLinkCommand> result =
            new SetPasswordWithLinkValidator().TestValidate(new SetPasswordWithLinkCommand("token", new string('a', length)));

        result.IsValid.Should().Be(valid);
        if (!valid)
        {
            result.ShouldHaveValidationErrorFor(c => c.Password);
        }
    }

    [Fact]
    public void Accepts_a_password_with_only_lowercase_letters_and_spaces()
    {
        new SetPasswordWithLinkValidator().TestValidate(new SetPasswordWithLinkCommand("token", "only lower case words"))
            .IsValid.Should().BeTrue();
    }
}
