using AwesomeAssertions;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;
using ClockIn.Tests.Common;

namespace ClockIn.Domain.Tests.Accounts;

/// <summary>create-user, change-role, deactivate, reactivate, unlock, change-own-password and update-preferences.</summary>
public sealed class UserAdministrationTests
{
    private static readonly DateTimeOffset Now = CompanyClock.At("2026-10-30 10:00");

    private readonly User _sam = User.Create(new Email("sam@clockin.test"), "Sam", Role.Supervisor);
    private readonly User _sid = User.Create(new Email("sid@clockin.test"), "Sid", Role.Supervisor);
    private readonly Team _warehouse;
    private readonly User _eve = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);

    public UserAdministrationTests()
    {
        _warehouse = Team.Create("Warehouse", _sam);
        _eve.MoveTo(_warehouse);
        _eve.SetPassword("hash");
    }

    [Fact]
    public void Creates_users_with_english_and_24h_unless_told_otherwise()
    {
        var fay = User.Create(new Email("fay@clockin.test"), "Fay", Role.Employee, PreferencesConstants.Spanish, PreferencesConstants.TwelveHour);

        (_eve.Language, _eve.TimeFormat).Should().Be(("en", "24h"));
        (fay.Language, fay.TimeFormat).Should().Be(("es", "12h"));
    }

    [Fact]
    public void Rejects_unsupported_preferences()
    {
        var language = () => _eve.UpdatePreferences("fr", "24h");
        var format = () => _eve.UpdatePreferences("en", "24");

        language.Should().Throw<ArgumentException>();
        format.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Updates_preferences_without_ending_sessions()
    {
        Guid stamp = _eve.SecurityStamp;

        _eve.UpdatePreferences("es", "12h");

        _eve.SecurityStamp.Should().Be(stamp);
        _eve.Language.Should().Be("es");
    }

    [Fact]
    public void Promotes_an_employee_to_supervisor_without_a_team_and_ends_sessions()
    {
        Guid stamp = _eve.SecurityStamp;

        _eve.ChangeRole(Role.Supervisor, null);

        _eve.Role.Should().Be(Role.Supervisor);
        _eve.TeamId.Should().BeNull();
        _eve.SecurityStamp.Should().NotBe(stamp);
    }

    [Fact]
    public void Demotes_a_supervisor_into_a_team()
    {
        _sid.ChangeRole(Role.Employee, _warehouse);

        _sid.Role.Should().Be(Role.Employee);
        _sid.TeamId.Should().Be(_warehouse.Id);
    }

    [Theory]
    [InlineData(Role.Employee, Role.Admin)]
    [InlineData(Role.Admin, Role.Supervisor)]
    [InlineData(Role.Admin, Role.Employee)]
    public void Never_grants_or_removes_admin(Role from, Role to)
    {
        var user = User.Create(new Email("x@clockin.test"), "X", from);

        var act = () => user.ChangeRole(to, _warehouse);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("AdminRoleIsFixed");
    }

    [Fact]
    public void Rejects_a_role_change_for_a_deactivated_user()
    {
        _eve.Deactivate();

        var act = () => _eve.ChangeRole(Role.Supervisor, null);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsDeactivated");
    }

    [Fact]
    public void Deactivates_once_and_voids_the_access_link()
    {
        var fay = User.Create(new Email("fay@clockin.test"), "Fay", Role.Supervisor);
        AccessLink link = fay.IssueAccessLink(Now);

        fay.Deactivate();
        var again = () => fay.Deactivate();
        var useLink = () => fay.SetPasswordWithLink(link.Token.Split('.')[1], "hash", Now);

        fay.Status.Should().Be(UserStatus.Deactivated);
        fay.AccessLinkHash.Should().BeNull();
        again.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsDeactivated");
        useLink.Should().Throw<DomainException>().Which.Code.Should().Be("InvalidOrExpiredLink");
    }

    [Fact]
    public void Reactivates_with_a_fresh_link_and_forgets_the_old_password()
    {
        _eve.Deactivate();

        AccessLink link = _eve.Reactivate(_warehouse, Now);

        _eve.Status.Should().Be(UserStatus.PendingActivation);
        _eve.PasswordHash.Should().BeNull();
        link.ExpiresAt.Should().Be(Now + AccessLink.Validity);
    }

    [Fact]
    public void Requires_an_active_team_to_reactivate_an_employee()
    {
        var oldStore = Team.Create("Old Store", _sid);
        oldStore.Archive();
        _eve.Deactivate();

        var act = () => _eve.Reactivate(oldStore, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("TeamIsArchived");
    }

    [Fact]
    public void Rejects_reactivating_someone_active()
    {
        var act = () => _eve.Reactivate(_warehouse, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("UserIsNotDeactivated");
    }

    [Fact]
    public void Unlocks_a_locked_user()
    {
        for (int i = 0; i < User.MaxFailedSignIns; i++)
        {
            _eve.SignIn(passwordMatches: false, Now);
        }

        _eve.IsLockedOut(Now).Should().BeTrue();
        _eve.Unlock();

        _eve.IsLockedOut(Now).Should().BeFalse();
        _eve.FailedSignIns.Should().Be(0);
        _eve.SignIn(passwordMatches: true, Now).Should().Be(SignInOutcome.SignedIn);
    }

    [Fact]
    public void Changes_the_password_only_with_the_current_one()
    {
        Guid stamp = _eve.SecurityStamp;

        _eve.ChangePassword(currentPasswordMatches: false, "new hash", Now).Should().Be(PasswordChangeOutcome.Rejected);
        _eve.PasswordHash.Should().Be("hash");
        _eve.FailedSignIns.Should().Be(1);

        _eve.ChangePassword(currentPasswordMatches: true, "new hash", Now).Should().Be(PasswordChangeOutcome.Changed);
        _eve.PasswordHash.Should().Be("new hash");
        _eve.SecurityStamp.Should().NotBe(stamp);
        _eve.FailedSignIns.Should().Be(0);
    }

    [Fact]
    public void Locks_the_account_on_the_fifth_wrong_current_password()
    {
        for (int i = 1; i < User.MaxFailedSignIns; i++)
        {
            _eve.SignIn(passwordMatches: false, Now);
        }

        _eve.ChangePassword(currentPasswordMatches: false, "new hash", Now).Should().Be(PasswordChangeOutcome.LockedOut);
        _eve.LockedUntil.Should().Be(Now + User.LockoutLength);
        _eve.ChangePassword(currentPasswordMatches: true, "new hash", Now).Should().Be(PasswordChangeOutcome.Rejected);
        _eve.PasswordHash.Should().Be("hash");
    }
}
