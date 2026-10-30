using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Tests.Common;

namespace ClockIn.Domain.Tests.Accounts;

/// <summary>log-in.feature and set-password-with-link.feature: the rules the User aggregate owns.</summary>
public sealed class UserSignInTests
{
    private static readonly DateTimeOffset Now = CompanyClock.At("2026-10-30 10:00");

    [Fact]
    public void Creates_a_pending_user_with_no_password()
    {
        var user = User.Create(new Email("fay@clockin.test"), "Fay", Role.Employee);

        user.Status.Should().Be(UserStatus.PendingActivation);
        user.PasswordHash.Should().BeNull();
        user.Name.Should().Be("Fay");
        user.Role.Should().Be(Role.Employee);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Rejects_a_blank_name(string name)
    {
        var act = () => User.Create(new Email("fay@clockin.test"), name, Role.Employee);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_a_name_longer_than_100_characters()
    {
        var act = () => User.Create(new Email("fay@clockin.test"), new string('x', 101), Role.Employee);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Activates_the_user_and_changes_the_stamp_when_a_password_is_set()
    {
        var user = User.Create(new Email("fay@clockin.test"), "Fay", Role.Employee);
        Guid stampBefore = user.SecurityStamp;

        user.SetPassword("hash");

        user.Status.Should().Be(UserStatus.Active);
        user.PasswordHash.Should().Be("hash");
        user.SecurityStamp.Should().NotBe(stampBefore);
    }

    [Fact]
    public void Signs_in_when_active_and_the_password_matches()
    {
        User eve = ActiveUser();

        eve.SignIn(passwordMatches: true, Now).Should().Be(SignInOutcome.SignedIn);
    }

    [Fact]
    public void Rejects_the_sign_in_when_the_password_is_wrong()
    {
        User eve = ActiveUser();

        eve.SignIn(passwordMatches: false, Now).Should().Be(SignInOutcome.Rejected);
    }

    [Fact]
    public void Rejects_the_sign_in_when_pending_activation()
    {
        var fay = User.Create(new Email("fay@clockin.test"), "Fay", Role.Employee);

        fay.SignIn(passwordMatches: true, Now).Should().Be(SignInOutcome.Rejected);
    }

    [Fact]
    public void Rejects_the_sign_in_when_deactivated()
    {
        User ed = ActiveUser();
        ed.Deactivate();

        ed.SignIn(passwordMatches: true, Now).Should().Be(SignInOutcome.Rejected);
    }

    [Fact]
    public void Locks_out_for_15_minutes_on_the_fifth_consecutive_failure()
    {
        User eve = FailedTimes(4);

        SignInOutcome outcome = eve.SignIn(passwordMatches: false, Now);

        outcome.Should().Be(SignInOutcome.LockedOut);
        eve.LockedUntil.Should().Be(Now.AddMinutes(15));
    }

    [Fact]
    public void Stays_locked_out_when_the_password_is_correct_before_15_minutes()
    {
        User eve = FailedTimes(5);

        eve.SignIn(passwordMatches: true, Now.AddMinutes(14).AddSeconds(59)).Should().Be(SignInOutcome.Rejected);
    }

    [Fact]
    public void Signs_in_when_the_lockout_has_passed()
    {
        User eve = FailedTimes(5);

        eve.SignIn(passwordMatches: true, Now.AddMinutes(15)).Should().Be(SignInOutcome.SignedIn);
        eve.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void Resets_the_failure_count_when_a_sign_in_succeeds()
    {
        User eve = FailedTimes(4);
        eve.SignIn(passwordMatches: true, Now);
        for (int i = 0; i < 4; i++)
        {
            eve.SignIn(passwordMatches: false, Now);
        }

        eve.SignIn(passwordMatches: true, Now).Should().Be(SignInOutcome.SignedIn);
    }

    [Fact]
    public void Clears_a_lockout_when_a_password_is_set()
    {
        User eve = FailedTimes(5);

        eve.SetPassword("new hash");

        eve.SignIn(passwordMatches: true, Now).Should().Be(SignInOutcome.SignedIn);
    }

    [Fact]
    public void Refuses_a_new_password_when_deactivated()
    {
        User ed = ActiveUser();
        ed.Deactivate();

        var act = () => ed.SetPassword("hash");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsDeactivated");
    }

    private static User ActiveUser()
    {
        var user = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);
        user.SetPassword("hash");
        return user;
    }

    private static User FailedTimes(int failures)
    {
        User user = ActiveUser();
        for (int i = 0; i < failures; i++)
        {
            user.SignIn(passwordMatches: false, Now);
        }

        return user;
    }
}
