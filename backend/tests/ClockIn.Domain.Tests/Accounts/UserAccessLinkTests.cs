using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Tests.Common;

namespace ClockIn.Domain.Tests.Accounts;

/// <summary>generate-access-link.feature and set-password-with-link.feature: the rules the User aggregate owns.</summary>
public sealed class UserAccessLinkTests
{
    private static readonly DateTimeOffset Now = CompanyClock.At("2026-10-30 10:00");

    [Fact]
    public void Issues_a_link_valid_for_72_hours_and_stores_only_its_hash()
    {
        User fay = PendingUser();

        AccessLink link = fay.IssueAccessLink(Now);

        link.UserId.Should().Be(fay.Id);
        link.ExpiresAt.Should().Be(Now.AddHours(72));
        fay.AccessLinkHash.Should().NotBeNull().And.NotContain(link.Secret);
    }

    [Fact]
    public void Activates_a_pending_user_and_uses_up_the_link()
    {
        User fay = PendingUser();
        AccessLink link = fay.IssueAccessLink(Now);

        fay.SetPasswordWithLink(link.Secret, "hash", Now.AddHours(1));

        fay.Status.Should().Be(UserStatus.Active);
        fay.PasswordHash.Should().Be("hash");
        fay.AccessLinkHash.Should().BeNull();
        var reuse = () => fay.SetPasswordWithLink(link.Secret, "other", Now.AddHours(2));
        reuse.Should().Throw<DomainException>().Which.Code.Should().Be("InvalidOrExpiredLink");
    }

    [Fact]
    public void Accepts_a_link_just_before_it_expires()
    {
        User fay = PendingUser();
        AccessLink link = fay.IssueAccessLink(Now);

        fay.SetPasswordWithLink(link.Secret, "hash", Now.AddHours(71).AddMinutes(59));

        fay.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Rejects_a_link_once_it_expired()
    {
        User fay = PendingUser();
        AccessLink link = fay.IssueAccessLink(Now);

        var act = () => fay.SetPasswordWithLink(link.Secret, "hash", Now.AddHours(72).AddSeconds(1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("InvalidOrExpiredLink");
    }

    [Fact]
    public void Rejects_an_earlier_link_once_a_newer_one_was_issued()
    {
        User fay = PendingUser();
        AccessLink first = fay.IssueAccessLink(Now);
        fay.IssueAccessLink(Now.AddMinutes(5));

        var act = () => fay.SetPasswordWithLink(first.Secret, "hash", Now.AddMinutes(10));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("InvalidOrExpiredLink");
    }

    [Fact]
    public void Rejects_the_link_when_the_user_was_deactivated_after_it_was_issued()
    {
        User ed = PendingUser();
        AccessLink link = ed.IssueAccessLink(Now);
        ed.Deactivate();

        var act = () => ed.SetPasswordWithLink(link.Secret, "hash", Now.AddMinutes(1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("InvalidOrExpiredLink");
    }

    [Fact]
    public void Refuses_to_issue_a_link_for_a_deactivated_user()
    {
        User ed = PendingUser();
        ed.Deactivate();

        var act = () => ed.IssueAccessLink(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsDeactivated");
    }

    [Fact]
    public void Keeps_the_current_password_working_until_a_reset_link_is_used()
    {
        User eve = PendingUser();
        eve.SetPassword("old hash");
        Guid stamp = eve.SecurityStamp;

        eve.IssueAccessLink(Now);

        eve.PasswordHash.Should().Be("old hash");
        eve.SecurityStamp.Should().Be(stamp, "issuing a link does not end sessions");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-dot")]
    [InlineData("not-a-guid.secret")]
    public void Rejects_a_malformed_token(string value)
    {
        AccessLink.TryParse(value, out _, out _).Should().BeFalse();
    }

    private static User PendingUser() => User.Create(new Email("fay@clockin.test"), "Fay", Role.Employee);
}
