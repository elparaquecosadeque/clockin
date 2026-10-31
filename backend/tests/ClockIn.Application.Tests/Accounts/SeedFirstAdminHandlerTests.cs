using AwesomeAssertions;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Tests.Common;
using NSubstitute;

namespace ClockIn.Application.Tests.Accounts;

/// <summary>seed-first-admin.feature, as the use case sees it.</summary>
public sealed class SeedFirstAdminHandlerTests
{
    private readonly CompanyClock _clock = new();
    private readonly IUserRepository _usersRepository = Substitute.For<IUserRepository>();
    private readonly IAuditLog _audit = Substitute.For<IAuditLog>();
    private readonly SeedFirstAdminHandler _handler;

    public SeedFirstAdminHandlerTests()
    {
        _usersRepository.FindByRoleAsync(Role.Admin, Arg.Any<CancellationToken>()).Returns([]);
        _handler = new SeedFirstAdminHandler(_usersRepository, _audit, _clock);
    }

    [Fact]
    public async Task Creates_a_pending_admin_and_logs_an_activation_link_when_there_are_no_admins()
    {
        await _handler.HandleAsync(new SeedFirstAdminCommand("admin@clockin.test"), CancellationToken.None);

        await _usersRepository.Received(1).SaveOrUpdateAsync(
            Arg.Is<User>(u => u.Email == new Email("admin@clockin.test") && u.Role == Role.Admin
                && u.Status == UserStatus.PendingActivation && u.AccessLinkHash != null),
            Arg.Any<CancellationToken>());
        _audit.Received(1).FirstAdminLinkIssued(new Email("admin@clockin.test"), Arg.Any<string>());
    }

    [Theory]
    [InlineData("admin@clockin.test")]
    [InlineData("someone-else@clockin.test")]
    [InlineData(null)]
    public async Task Does_nothing_when_an_active_admin_exists(string? configuredEmail)
    {
        var ada = User.Create(new Email("ada@clockin.test"), "Ada", Role.Admin);
        ada.SetPassword("hash");
        _usersRepository.FindByRoleAsync(Role.Admin, Arg.Any<CancellationToken>()).Returns([ada]);

        await _handler.HandleAsync(new SeedFirstAdminCommand(configuredEmail), CancellationToken.None);

        await _usersRepository.DidNotReceiveWithAnyArgs().SaveOrUpdateAsync(default!, TestContext.Current.CancellationToken);
        _audit.DidNotReceiveWithAnyArgs().FirstAdminLinkIssued(default!, default!);
    }

    [Fact]
    public async Task Issues_a_fresh_link_when_the_seeded_admin_has_not_activated_yet()
    {
        var seeded = User.Create(new Email("admin@clockin.test"), "Admin", Role.Admin);
        AccessLink previous = seeded.IssueAccessLink(_clock.GetUtcNow());
        _usersRepository.FindByRoleAsync(Role.Admin, Arg.Any<CancellationToken>()).Returns([seeded]);

        await _handler.HandleAsync(new SeedFirstAdminCommand("admin@clockin.test"), CancellationToken.None);

        _audit.Received(1).FirstAdminLinkIssued(seeded.Email, Arg.Is<string>(t => t != previous.Token));
        await _usersRepository.Received(1).SaveOrUpdateAsync(seeded, Arg.Any<CancellationToken>());
        var usePrevious = () => seeded.SetPasswordWithLink(previous.Secret, "hash", _clock.GetUtcNow());
        usePrevious.Should().Throw<Domain.Common.DomainException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Fails_naming_the_setting_when_there_are_no_admins_and_no_email(string? configuredEmail)
    {
        var act = () => _handler.HandleAsync(new SeedFirstAdminCommand(configuredEmail), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*FirstAdmin:Email*");
    }
}
