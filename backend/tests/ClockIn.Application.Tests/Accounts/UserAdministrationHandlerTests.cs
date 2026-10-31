using AwesomeAssertions;
using ClockIn.Application.Accounts.Commands;
using ClockIn.Application.Accounts.Interfaces;
using ClockIn.Application.Accounts.Results;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Tests.Timesheets;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Common;
using ClockIn.Domain.Teams;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;
using FluentValidation.TestHelper;
using NSubstitute;

namespace ClockIn.Application.Tests.Accounts;

/// <summary>
/// create-user, change-role, deactivate-user, reactivate-user, unlock-account, change-own-password and
/// update-preferences, as the use cases see them. Admin-only commands are gated by the permission policy.
/// </summary>
public sealed class UserAdministrationHandlerTests
{
    private readonly TimesheetWorld _world = new();
    private readonly IAuditLog _audit = Substitute.For<IAuditLog>();
    private readonly ISessionRepository _sessionsRepository = Substitute.For<ISessionRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    public UserAdministrationHandlerTests()
    {
        _world.SignIn(_world.Ada);
        _world.TeamsRepository.LeadsActiveTeamAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<UserId>() == _world.Sam.Id || c.Arg<UserId>() == _world.Sue.Id);
        _hasher.Hash(Arg.Any<string>()).Returns(c => "hash:" + c.Arg<string>());
        _hasher.Verify(Arg.Any<string?>(), Arg.Any<string>()).Returns(c => c.ArgAt<string?>(0) == "hash:" + c.ArgAt<string>(1));
    }

    private static CancellationToken Ct => CancellationToken.None;

    [Fact]
    public async Task Creates_an_employee_in_a_team_with_an_activation_link()
    {
        AccessLinkDto link = await CreateUser().HandleAsync(
            new CreateUserCommand(" Fay@ClockIn.Test ", "Fay", "Employee", _world.Warehouse.Id.Value, "es", "12h"), Ct);

        User fay = SavedUser();
        (fay.Email.Value, fay.Role, fay.TeamId, fay.Status).Should().Be(("fay@clockin.test", Role.Employee, _world.Warehouse.Id, UserStatus.PendingActivation));
        (fay.Language, fay.TimeFormat).Should().Be(("es", "12h"));
        link.ExpiresAt.Should().Be(_world.Clock.GetUtcNow() + AccessLink.Validity);
        _audit.Received(1).AccessLinkGenerated(_world.Ada.Id, fay.Id);
    }

    [Fact]
    public async Task Rejects_an_archived_or_unknown_team()
    {
        var oldStore = Team.Create("Old Store", User.Create(new Email("sid@clockin.test"), "Sid", Role.Supervisor));
        oldStore.Archive();
        _world.TeamsRepository.FindAsync(oldStore.Id, Arg.Any<CancellationToken>()).Returns(oldStore);

        var archived = () => CreateUser().HandleAsync(new CreateUserCommand("fay@clockin.test", "Fay", "Employee", oldStore.Id.Value), Ct);
        var unknown = () => CreateUser().HandleAsync(new CreateUserCommand("fay@clockin.test", "Fay", "Employee", Guid.CreateVersion7()), Ct);

        (await archived.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("TeamIsArchived");
        await unknown.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("Email", "", "Employee", true, "en", "24h")]
    [InlineData("Email", "not-an-email", "Employee", true, "en", "24h")]
    [InlineData("Name", "fay@clockin.test", "Employee", true, "en", "24h")]
    [InlineData("Role", "fay@clockin.test", "Manager", false, "en", "24h")]
    [InlineData("Language", "fay@clockin.test", "Employee", true, "fr", "24h")]
    [InlineData("TimeFormat", "fay@clockin.test", "Employee", true, "en", "36h")]
    [InlineData("TeamId", "fay@clockin.test", "Employee", false, "en", "24h")]
    [InlineData("TeamId", "fay@clockin.test", "Supervisor", true, "en", "24h")]
    [InlineData("TeamId", "fay@clockin.test", "Admin", true, "en", "24h")]
    public void Validates_new_users(string field, string email, string role, bool withTeam, string language, string timeFormat)
    {
        string name = field == "Name" ? " " : "Fay";
        var command = new CreateUserCommand(email, name, role, withTeam ? Guid.CreateVersion7() : null, language, timeFormat);

        new CreateUserValidator().TestValidate(command).ShouldHaveValidationErrorFor(field);
    }

    [Fact]
    public async Task Promotes_an_employee_and_audits_it()
    {
        await ChangeRole().HandleAsync(new ChangeRoleCommand(_world.Eve.Id.Value, "Supervisor", null), Ct);

        (_world.Eve.Role, _world.Eve.TeamId).Should().Be((Role.Supervisor, (TeamId?)null));
        await _world.UsersRepository.Received(1).SaveOrUpdateAsync(_world.Eve, Ct);
        _audit.Received(1).RoleChanged(_world.Ada.Id, _world.Eve.Id, Role.Supervisor);
    }

    [Fact]
    public async Task Rejects_demoting_a_supervisor_who_leads_a_team()
    {
        var act = () => ChangeRole().HandleAsync(new ChangeRoleCommand(_world.Sam.Id.Value, "Employee", _world.Office.Id.Value), Ct);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("TeamNeedsNewSupervisor");
    }

    [Fact]
    public async Task Rejects_making_an_admin_by_role_change()
    {
        var act = () => ChangeRole().HandleAsync(new ChangeRoleCommand(_world.Ada.Id.Value, "Supervisor", null), Ct);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("AdminRoleIsFixed");
    }

    [Fact]
    public void Requires_a_team_only_for_employees()
    {
        new ChangeRoleValidator().TestValidate(new ChangeRoleCommand(Guid.CreateVersion7(), "Employee", null))
            .ShouldHaveValidationErrorFor(c => c.TeamId);
        new ChangeRoleValidator().TestValidate(new ChangeRoleCommand(Guid.CreateVersion7(), "Supervisor", Guid.CreateVersion7()))
            .ShouldHaveValidationErrorFor(c => c.TeamId);
    }

    [Fact]
    public async Task Deactivates_revokes_sessions_and_audits_it()
    {
        await Deactivate().HandleAsync(new DeactivateUserCommand(_world.Eve.Id.Value), Ct);

        _world.Eve.Status.Should().Be(UserStatus.Deactivated);
        await _sessionsRepository.Received(1).RevokeAllAsync(_world.Eve.Id, _world.Clock.GetUtcNow(), Ct);
        _audit.Received(1).UserDeactivated(_world.Ada.Id, _world.Eve.Id);
    }

    [Fact]
    public async Task Leaves_nothing_dangling_when_deactivating()
    {
        var open = Timesheet.Start(_world.Ed.Id, Week.Containing(_world.Clock.GetUtcNow(), CompanyClock.Zone));
        open.ClockIn(_world.Clock.GetUtcNow().AddHours(-2), TimesheetWorld.Phone, CompanyClock.Zone);
        _world.Add(open);

        var self = () => Deactivate().HandleAsync(new DeactivateUserCommand(_world.Ada.Id.Value), Ct);
        var clockedIn = () => Deactivate().HandleAsync(new DeactivateUserCommand(_world.Ed.Id.Value), Ct);
        var leader = () => Deactivate().HandleAsync(new DeactivateUserCommand(_world.Sam.Id.Value), Ct);

        (await self.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("CannotDeactivateSelf");
        (await clockedIn.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("OpenEntryMustBeClosed");
        (await leader.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("TeamNeedsNewSupervisor");
    }

    [Fact]
    public async Task Reactivates_into_the_old_team_or_the_chosen_one()
    {
        _world.Eve.Deactivate();
        _world.Ed.Deactivate();

        AccessLinkDto link = await Reactivate().HandleAsync(new ReactivateUserCommand(_world.Eve.Id.Value, null), Ct);
        await Reactivate().HandleAsync(new ReactivateUserCommand(_world.Ed.Id.Value, _world.Office.Id.Value), Ct);

        (_world.Eve.Status, _world.Eve.TeamId).Should().Be((UserStatus.PendingActivation, _world.Warehouse.Id));
        _world.Ed.TeamId.Should().Be(_world.Office.Id);
        link.Token.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Unlocks_a_user()
    {
        for (int i = 0; i < User.MaxFailedSignIns; i++)
        {
            _world.Eve.SignIn(passwordMatches: false, _world.Clock.GetUtcNow());
        }

        await new UnlockUserHandler(_world.UsersRepository).HandleAsync(new UnlockUserCommand(_world.Eve.Id.Value), Ct);

        _world.Eve.IsLockedOut(_world.Clock.GetUtcNow()).Should().BeFalse();
        await _world.UsersRepository.Received(1).SaveOrUpdateAsync(_world.Eve, Ct);
    }

    [Fact]
    public async Task Changes_the_own_password_and_starts_a_fresh_session()
    {
        _world.SignIn(_world.Eve);
        _world.Eve.SetPassword("hash:old password!!");

        SessionResult session = await ChangePassword().HandleAsync(new ChangeOwnPasswordCommand("old password!!", "a new long password"), Ct);

        _world.Eve.PasswordHash.Should().Be("hash:a new long password");
        session.UserId.Should().Be(_world.Eve.Id.Value);
        session.SecurityStamp.Should().Be(_world.Eve.SecurityStamp);
        Received.InOrder(() =>
        {
            _sessionsRepository.RevokeAllAsync(_world.Eve.Id, Arg.Any<DateTimeOffset>(), Ct);
            _sessionsRepository.SaveOrUpdateAsync(Arg.Any<Session>(), Ct);
        });
    }

    [Fact]
    public async Task Rejects_a_wrong_current_password()
    {
        _world.SignIn(_world.Eve);
        _world.Eve.SetPassword("hash:old password!!");

        var act = () => ChangePassword().HandleAsync(new ChangeOwnPasswordCommand("wrong", "a new long password"), Ct);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("InvalidCurrentPassword");
        _world.Eve.PasswordHash.Should().Be("hash:old password!!");
    }

    [Fact]
    public void Applies_the_password_policy()
    {
        new ChangeOwnPasswordValidator().TestValidate(new ChangeOwnPasswordCommand("current", new string('x', 11)))
            .ShouldHaveValidationErrorFor(c => c.NewPassword);
    }

    [Fact]
    public async Task Updates_own_preferences()
    {
        _world.SignIn(_world.Eve);

        await new UpdatePreferencesHandler(_world.CurrentUser, _world.UsersRepository).HandleAsync(new UpdatePreferencesCommand("es", "12h"), Ct);

        (_world.Eve.Language, _world.Eve.TimeFormat).Should().Be(("es", "12h"));
    }

    [Theory]
    [InlineData("pt", "24h", "Language")]
    [InlineData("", "24h", "Language")]
    [InlineData("en", "24", "TimeFormat")]
    public void Rejects_unsupported_preferences(string language, string timeFormat, string field)
    {
        new UpdatePreferencesValidator().TestValidate(new UpdatePreferencesCommand(language, timeFormat)).ShouldHaveValidationErrorFor(field);
    }

    private CreateUserHandler CreateUser() => new(_world.CurrentUser, _world.UsersRepository, _world.TeamsRepository, _audit, _world.Clock);

    private ChangeRoleHandler ChangeRole() => new(_world.CurrentUser, _world.UsersRepository, _world.TeamsRepository, _audit);

    private DeactivateUserHandler Deactivate() =>
        new(_world.CurrentUser, _world.UsersRepository, _sessionsRepository, _world.TeamsRepository, _world.TimesheetsRepository, _audit, _world.Clock);

    private ReactivateUserHandler Reactivate() => new(_world.CurrentUser, _world.UsersRepository, _world.TeamsRepository, _audit, _world.Clock);

    private ChangeOwnPasswordHandler ChangePassword() => new(_world.CurrentUser, _world.UsersRepository, _sessionsRepository, _hasher, _world.Clock);

    private User SavedUser() => (User)_world.UsersRepository.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "SaveOrUpdateAsync").GetArguments()[0]!;
}
